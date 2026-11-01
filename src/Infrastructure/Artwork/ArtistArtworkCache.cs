using System.Threading.Channels;
using LinerNotes.Infrastructure.Caching;
using LinerNotes.Application.Common.Interfaces.Discovery;
using Microsoft.Extensions.Caching.Memory;

namespace LinerNotes.Infrastructure.Artwork;

// Public photos share the existing metadata budget; no taste or account data is retained.
public sealed class ArtistArtworkCache(TimeProvider clock, DiscoveryMemoryCaches caches) : IArtistArtwork
{
    private readonly MemoryCache cache = caches.Metadata;
    private readonly Channel<Guid> queue = Channel.CreateBounded<Guid>(128);
    private readonly HashSet<Guid> pending = [];
    private readonly object sync = new();
    private sealed record Entry(ArtistPhoto? Photo, DateTimeOffset Expires, bool Retryable);

    public ArtistPhoto? GetCached(Guid id)
    {
        lock (sync) return cache.TryGetValue("artist-photo:" + id, out Entry? entry) && entry!.Expires > clock.GetUtcNow() ? entry.Photo : null;
    }

    public RecordingArtworkState Request(Guid id)
    {
        if (id == Guid.Empty) return RecordingArtworkState.Unavailable;
        lock (sync)
        {
            if (cache.TryGetValue("artist-photo:" + id, out Entry? entry) && entry!.Expires > clock.GetUtcNow())
                return entry.Photo is not null ? RecordingArtworkState.Ready : entry.Retryable ? RecordingArtworkState.Pending : RecordingArtworkState.Unavailable;
            if (!pending.Contains(id) && queue.Writer.TryWrite(id)) pending.Add(id);
            return RecordingArtworkState.Pending;
        }
    }

    public ValueTask<Guid> TakeAsync(CancellationToken ct) => queue.Reader.ReadAsync(ct);
    public void Complete(Guid id, ArtistPhoto? photo, bool retryable = false)
    {
        lock (sync)
        {
            var expiry = photo?.ExpiresAtUtc ?? clock.GetUtcNow().AddSeconds(retryable ? 30 : 3600);
            cache.Set("artist-photo:" + id, new Entry(photo, expiry, retryable), new MemoryCacheEntryOptions
                { AbsoluteExpiration = expiry, Size = DiscoveryMemoryCaches.EstimateBytes((photo?.Bytes.Length ?? 0) + (photo?.Credit.Length ?? 0) * 2L + 1024, "artist-photo:" + id) });
            pending.Remove(id);
        }
    }
}
