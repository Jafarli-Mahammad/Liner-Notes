using System.Diagnostics;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using Xunit;

namespace LinerNotes.Infrastructure.Tests.RecommendationSources;

public sealed class LastFmRateLimiterTests
{
    [Fact]
    public async Task AcquireAsync_AllowsInitialBurstUpToCapacity()
    {
        // Arrange
        using var limiter = new LastFmRateLimiter(requestsPerSecond: 4);

        // Act & Assert - acquiring initial tokens up to capacity should complete quickly without blocking
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 4; i++)
        {
            await limiter.AcquireAsync();
        }
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 500, $"Initial burst took too long: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task AcquireAsync_ThrottlesWhenCapacityExhausted()
    {
        // Arrange: 2 req/sec means 500ms per token refill
        using var limiter = new LastFmRateLimiter(requestsPerSecond: 2);

        // Exhaust initial capacity (2 tokens)
        await limiter.AcquireAsync();
        await limiter.AcquireAsync();

        // Act - 3rd acquire must wait for token replenishment
        var sw = Stopwatch.StartNew();
        await limiter.AcquireAsync();
        sw.Stop();

        // Assert: wait should be approximately >= 300ms
        Assert.True(sw.ElapsedMilliseconds >= 250, $"Expected throttling delay >= 250ms, but was {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Constructor_ThrowsOnZeroOrNegativeRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LastFmRateLimiter(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LastFmRateLimiter(-5));
    }
}
