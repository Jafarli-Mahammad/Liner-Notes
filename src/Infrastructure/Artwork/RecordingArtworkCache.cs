using LinerNotes.Application.Common.Interfaces.Discovery;
using Microsoft.Extensions.Caching.Memory;

namespace LinerNotes.Infrastructure.Artwork;

internal sealed record ArtworkCacheEntry(RecordingCover? Cover, bool Retryable);

public sealed class RecordingArtworkCache : IArtworkQueue, IDisposable
{
    // Separate byte budget: images must not evict recommendation evidence.
    internal MemoryCache Entries { get; } = new(new MemoryCacheOptions { SizeLimit = 16 * 1024 * 1024 });
    private readonly object sync = new();
    private readonly LinkedList<Guid> requested = new(), maintenance = new();
    private readonly Dictionary<Guid, LinkedListNode<Guid>> queued = new();
    private readonly HashSet<Guid> working = [];
    private readonly SemaphoreSlim signal = new(0, 1);
    internal SemaphoreSlim[] Gates { get; } = Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public RecordingCover? Get(Guid id) => Entries.Get<ArtworkCacheEntry>("recording:" + id)?.Cover;
    internal bool Retryable(Guid id) => Entries.Get<ArtworkCacheEntry>("recording:" + id)?.Retryable == true;
    internal bool Contains(Guid id) => Entries.TryGetValue("recording:" + id, out _);

    public void Enqueue(IReadOnlyList<Guid> recordingIds, bool priority = false)
    {
        lock (sync)
        {
            foreach (var id in recordingIds)
            {
                if (id == Guid.Empty || Contains(id) || working.Contains(id)) continue;
                if (queued.TryGetValue(id, out var node))
                {
                    if (priority && node.List == maintenance) { maintenance.Remove(node); requested.AddLast(node); }
                    continue;
                }
                if (queued.Count + working.Count >= 256)
                {
                    if (!priority || maintenance.Last is not { } victim) continue;
                    maintenance.Remove(victim); queued.Remove(victim.Value);
                }
                queued.Add(id, (priority ? requested : maintenance).AddLast(id));
            }
            if (queued.Count > 0 && signal.CurrentCount == 0) signal.Release();
        }
    }

    public async Task<IReadOnlyList<Guid>> TakeAsync(CancellationToken ct)
    {
        while (true)
        {
            await signal.WaitAsync(ct);
            lock (sync)
            {
                var batch = new List<Guid>();
                while (batch.Count < 8 && queued.Count > 0)
                {
                    var list = requested.Count > 0 ? requested : maintenance;
                    var id = list.First!.Value;
                    list.RemoveFirst(); queued.Remove(id); working.Add(id); batch.Add(id);
                }
                if (queued.Count > 0 && signal.CurrentCount == 0) signal.Release();
                if (batch.Count > 0) return batch;
            }
        }
    }

    public void Complete(IReadOnlyList<Guid> recordingIds, bool retryable = false)
    {
        lock (sync)
            foreach (var id in recordingIds)
            {
                working.Remove(id);
                if (!Contains(id)) Entries.Set("recording:" + id, new ArtworkCacheEntry(null, retryable),
                    new MemoryCacheEntryOptions { Size = 128, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) });
            }
    }

    public void Dispose()
    {
        Entries.Dispose();
        signal.Dispose();
        foreach (var gate in Gates) gate.Dispose();
    }
}
