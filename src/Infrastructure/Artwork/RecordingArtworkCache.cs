using LinerNotes.Application.Common.Interfaces.Discovery;
using Microsoft.Extensions.Caching.Memory;

namespace LinerNotes.Infrastructure.Artwork;

public sealed class RecordingArtworkCache : IDisposable
{
    // Separate byte budget: images must not evict recommendation evidence.
    internal MemoryCache Entries { get; } = new(new MemoryCacheOptions { SizeLimit = 16 * 1024 * 1024 });
    internal SemaphoreSlim Gate { get; } = new(1);

    public RecordingCover? Get(Guid id) => Entries.Get<RecordingCover>("recording:" + id);

    public void Dispose()
    {
        Entries.Dispose();
        Gate.Dispose();
    }
}
