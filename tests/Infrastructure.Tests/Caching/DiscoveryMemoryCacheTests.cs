using LinerNotes.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace LinerNotes.Infrastructure.Tests.Caching;

public sealed class DiscoveryMemoryCacheTests
{
    [Fact]
    public void ConcurrentResponsesStayInsideTheirByteBudgetsAndOversizedEntriesAreNotRetained()
    {
        var options = new DiscoveryCacheOptions { LastFmBytes = 65_536, MetadataBytes = 65_536, PreparedBytes = 65_536, TotalBytes = 196_608 };
        using var caches = new DiscoveryMemoryCaches(options);
        Parallel.For(0, 100, i =>
        {
            string key = "bounded-public-fixture-" + i;
            long estimate = DiscoveryMemoryCaches.EstimateBytes(8192, key);
            caches.Metadata.Set(key, new byte[8192], new MemoryCacheEntryOptions { Size = estimate });
        });
        Assert.InRange(caches.Metadata.GetCurrentStatistics()!.CurrentEstimatedSize!.Value, 0, options.MetadataBytes);
        caches.Prepared.Set("oversized", new byte[20_000], new MemoryCacheEntryOptions
        { Size = DiscoveryMemoryCaches.EstimateBytes(20_000, "oversized") });
        Assert.False(caches.Prepared.TryGetValue("oversized", out _));
        caches.LastFm.Set("lastfm", new byte[1024], new MemoryCacheEntryOptions { Size = DiscoveryMemoryCaches.EstimateBytes(1024, "lastfm") });
        Assert.True(caches.LastFm.TryGetValue("lastfm", out _));
        Assert.Equal(options.TotalBytes, caches.MaximumBytes);
    }

    [Fact]
    public void MisconfiguredAggregateBudgetFailsBeforeCachesAreCreated()
    {
        Assert.Throws<ArgumentException>(() => new DiscoveryMemoryCaches(new DiscoveryCacheOptions
        { LastFmBytes = 16 * 1024 * 1024, MetadataBytes = 16 * 1024 * 1024, PreparedBytes = 64 * 1024 * 1024 }));
    }
}
