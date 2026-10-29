using System.Diagnostics;
using LinerNotes.Infrastructure.Caching;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

namespace LinerNotes.Infrastructure.Artwork;

internal sealed record ArtworkCacheEntry(RecordingCover? Cover, bool Retryable, DateTimeOffset? RetryAtUtc = null);

public sealed class RecordingArtworkCache(TimeProvider? clock = null) : IArtworkQueue, IDisposable
{
    internal MemoryCache Entries { get; } = new(new MemoryCacheOptions { SizeLimit = 16 * 1024 * 1024 });
    private readonly object sync = new();
    private readonly LinkedList<Guid> requested = new(), maintenance = new();
    private readonly Dictionary<Guid, LinkedListNode<Guid>> queued = new();
    private readonly Dictionary<Guid, long> enqueuedAt = new();
    private readonly HashSet<Guid> working = [], demanded = [], acquiring = [];
    private sealed record Prepared(ArtworkAcquisition Plan, long QueuedAt);
    private readonly LinkedList<Prepared> readyRequested = new(), readyMaintenance = new();
    private readonly Dictionary<Guid, LinkedListNode<Prepared>> prepared = new();
    private readonly SemaphoreSlim signal = new(0, 1), readySignal = new(0, 1);
    private int consecutiveDemand, consecutiveReadyDemand;
    internal SemaphoreSlim Downloads { get; } = new(2, 2);
    internal SemaphoreSlim Revalidation { get; } = new(1, 1);
    internal BoundedKeyedGate Releases { get; } = new(32);

    public RecordingCover? Get(Guid id) => Entries.Get<ArtworkCacheEntry>("recording:" + id)?.Cover;
    internal bool Retryable(Guid id) => Entries.Get<ArtworkCacheEntry>("recording:" + id)?.Retryable == true;
    internal DateTimeOffset? RetryAt(Guid id) => Entries.Get<ArtworkCacheEntry>("recording:" + id)?.RetryAtUtc;
    internal bool Contains(Guid id) => Entries.TryGetValue("recording:" + id, out _);

    public void Enqueue(IReadOnlyList<Guid> recordingIds, bool priority = false)
    {
        lock (sync)
        {
            foreach (var id in recordingIds)
            {
                if (id == Guid.Empty || Contains(id)) continue;
                if (working.Contains(id))
                {
                    if (priority)
                    {
                        demanded.Add(id);
                        if (prepared.TryGetValue(id, out var ready) && ready.List == readyMaintenance)
                        { readyMaintenance.Remove(ready); readyRequested.AddLast(ready); }
                    }
                    continue;
                }
                if (queued.TryGetValue(id, out var node))
                {
                    if (priority && node.List == maintenance) { maintenance.Remove(node); requested.AddLast(node); demanded.Add(id); }
                    continue;
                }
                if (queued.Count + working.Count >= 256)
                {
                    if (!priority) continue;
                    if (maintenance.Last is { } victim)
                    { maintenance.Remove(victim); queued.Remove(victim.Value); enqueuedAt.Remove(victim.Value); }
                    else if (readyMaintenance.Last is { } readyVictim)
                    {
                        var victimId = readyVictim.Value.Plan.RecordingId;
                        readyMaintenance.Remove(readyVictim); prepared.Remove(victimId); working.Remove(victimId); demanded.Remove(victimId);
                    }
                    else continue;
                }
                queued.Add(id, (priority ? requested : maintenance).AddLast(id));
                enqueuedAt.Add(id, Stopwatch.GetTimestamp());
                if (priority) demanded.Add(id);
            }
            Wake(signal, queued.Count);
        }
    }

    public async Task<IReadOnlyList<Guid>> TakeAsync(CancellationToken ct)
    {
        while (true)
        {
            await signal.WaitAsync(ct);
            var batch = new List<Guid>();
            var waits = new List<TimeSpan>();
            lock (sync)
            {
                while (batch.Count < 8 && queued.Count > 0)
                {
                    var list = requested.Count > 0 && (maintenance.Count == 0 || consecutiveDemand < 7) ? requested : maintenance;
                    consecutiveDemand = list == requested ? Math.Min(7, consecutiveDemand + 1) : 0;
                    var id = list.First!.Value;
                    list.RemoveFirst(); queued.Remove(id); working.Add(id); batch.Add(id);
                    if (enqueuedAt.Remove(id, out var at)) waits.Add(Stopwatch.GetElapsedTime(at));
                }
                Wake(signal, queued.Count);
            }
            foreach (var wait in waits) DiscoveryDiagnostics.RecordDuration("artwork.queue", wait);
            if (batch.Count > 0) return batch;
        }
    }

    public void QueuePrepared(IReadOnlyList<ArtworkAcquisition> acquisitions)
    {
        lock (sync)
        {
            foreach (var plan in acquisitions)
            {
                var id = plan.RecordingId;
                if (!working.Contains(id) || prepared.ContainsKey(id) || acquiring.Contains(id)) continue;
                var entry = new Prepared(plan, Stopwatch.GetTimestamp());
                prepared.Add(id, (demanded.Contains(id) ? readyRequested : readyMaintenance).AddLast(entry));
            }
            Wake(readySignal, prepared.Count);
        }
    }

    public async Task<ArtworkAcquisition> TakePreparedAsync(CancellationToken ct)
    {
        while (true)
        {
            await readySignal.WaitAsync(ct);
            Prepared? next = null;
            lock (sync)
            {
                if (prepared.Count > 0)
                {
                    var list = readyRequested.Count > 0 && (readyMaintenance.Count == 0 || consecutiveReadyDemand < 7)
                        ? readyRequested : readyMaintenance;
                    consecutiveReadyDemand = list == readyRequested ? Math.Min(7, consecutiveReadyDemand + 1) : 0;
                    next = list.First!.Value; list.RemoveFirst();
                    prepared.Remove(next.Plan.RecordingId); acquiring.Add(next.Plan.RecordingId);
                }
                Wake(readySignal, prepared.Count);
            }
            if (next is null) continue;
            DiscoveryDiagnostics.RecordDuration("artwork.image_queue", Stopwatch.GetElapsedTime(next.QueuedAt));
            return next.Plan;
        }
    }

    public void Complete(IReadOnlyList<Guid> recordingIds, bool retryable = false)
    {
        lock (sync)
            foreach (var id in recordingIds)
            {
                working.Remove(id); demanded.Remove(id); acquiring.Remove(id);
                if (prepared.Remove(id, out var node)) node.List!.Remove(node);
                if (!Contains(id)) Entries.Set("recording:" + id, new ArtworkCacheEntry(null, retryable,
                    retryable ? (clock ?? TimeProvider.System).GetUtcNow().AddMinutes(1) : null),
                    new MemoryCacheEntryOptions { Size = 128, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) });
            }
    }

    private static void Wake(SemaphoreSlim target, int count)
    { if (count > 0 && target.CurrentCount == 0) target.Release(); }

    public void Dispose()
    {
        Entries.Dispose(); signal.Dispose(); readySignal.Dispose(); Downloads.Dispose(); Revalidation.Dispose(); Releases.Dispose();
    }
}
