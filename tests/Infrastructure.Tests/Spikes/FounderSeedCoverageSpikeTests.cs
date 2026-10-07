using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LinerNotes.Infrastructure.Tests.Spikes;

// Developer-approved replacement: mechanics only, no catalog conclusions.
// No credential discovery, live fallback or tracked report writes.
public sealed class FounderSeedCoverageSpikeTests
{
    [Fact]
    public async Task AllFounderSeeds_UseFixturesWithoutOutboundAttempts_EvenWithCredentialConfigured()
    {
        string[] seeds = ["Jakuzi", "Son Feci Bisiklet", "M.O.O.N.", "Perturbator", "Jasper Byrne",
            "Paweł Błaszczak", "Heaven Pierce Her", "Darren Korb"];
        using var handler = new RejectOutboundHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new LastFmOptions { ApiKey = "dummy-offline-credential", Mode = LastFmClientMode.FixtureOnly });
        var client = new LastFmApiClient(http, options, cache, new LastFmRateLimiter(1), NullLogger<LastFmApiClient>.Instance);
        foreach (var seed in seeds)
        {
            var similar = await client.GetSimilarArtistsAsync(seed, limit: 5);
            var tags = await client.GetArtistTopTagsAsync(seed, limit: 15);
            var tracks = await client.GetArtistTopTracksAsync(seed, limit: 5);
            Assert.NotNull(similar); Assert.NotNull(tags); Assert.NotNull(tracks);
            Assert.True(similar.Count <= 5);
            Assert.True(tags.Count <= 15);
            Assert.True(tracks.Count <= 5);
            Assert.Equal(0, handler.Attempts);
        }
    }

    private sealed class RejectOutboundHandler : HttpMessageHandler
    {
        public int Attempts { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Attempts++;
            throw new InvalidOperationException("Outbound HTTP forbidden in founder fixture test.");
        }
    }
}
