using System.Net;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;
using LinerNotes.Infrastructure.Tests.Fakes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace LinerNotes.Infrastructure.Tests.RecommendationSources;

public sealed class Phase3IngestionTests
{
    [Fact]
    public async Task UnknownFixtureSeedsMustBeGapsWithoutSubstitutedArtistsOrTracks()
    {
        using var handler = new RejectOutbound();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new LastFmApiClient(http, Options.Create(new LastFmOptions { Mode = LastFmClientMode.FixtureOnly }),
            cache, new LastFmRateLimiter(100), NullLogger<LastFmApiClient>.Instance);
        Assert.Empty(await client.GetSimilarArtistsAsync("unknown synthetic seed"));
        Assert.Empty(await client.GetArtistTopTagsAsync("unknown synthetic seed"));
        Assert.Empty(await client.GetArtistTopTracksAsync("unknown synthetic seed"));
        Assert.Empty(await client.GetTagTopTracksAsync("unknown synthetic tag"));
    }

    [Fact]
    public async Task FailedHybridRequestMustNotSubstituteSyntheticEvidence()
    {
        using var handler = MockHttpMessageHandler.WithStatusCode(HttpStatusCode.TooManyRequests);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new LastFmApiClient(http, Options.Create(new LastFmOptions { Mode = LastFmClientMode.Hybrid, ApiKey = "dummy" }),
            cache, new LastFmRateLimiter(100), NullLogger<LastFmApiClient>.Instance);
        Assert.Empty(await client.GetSimilarArtistsAsync("Jakuzi"));
        Assert.Single(handler.RecordedRequests);
    }

    [Fact]
    public async Task MissingTagEvidenceMustRemainEmptyAndArtistPopularityMustRemainUnknown()
    {
        var api = Substitute.For<ILastFmApiClient>();
        api.GetArtistTopTagsAsync("artist", 15, Arg.Any<CancellationToken>()).Returns(EvidenceFixtures.Response(new List<LastFmTagItem>(), identity: "artist", method: "artist.gettoptags"));
        var hydrator = new LastFmCandidateHydrator(api, NullLogger<LastFmCandidateHydrator>.Instance);
        var candidate = await hydrator.HydrateCandidateAsync(EvidenceFixtures.Raw("track", "artist", null, .8));
        Assert.NotNull(candidate);
        Assert.True(candidate.TagVector.IsEmpty);
        Assert.Null(candidate.GlobalPopularity);
        await api.DidNotReceive().GetArtistTopTracksAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MissingMatchMustNotBecomeHalfSimilarity()
    {
        var api = Substitute.For<ILastFmApiClient>();
        api.GetSimilarArtistsAsync("seed", 10, Arg.Any<CancellationToken>()).Returns(EvidenceFixtures.Response(new List<LastFmArtistSummary> { new("artist", null, null, null) }, identity: "seed", method: "artist.getsimilar"));
        api.GetArtistTopTracksAsync("artist", 2, Arg.Any<CancellationToken>()).Returns(EvidenceFixtures.Response(new List<LastFmTrackItem> { new("track", null, null, null, null, null, null) }, identity: "artist", method: "artist.gettoptracks"));
        var source = new LastFmRecommendationSource(api, NullLogger<LastFmRecommendationSource>.Instance);
        var candidate = Assert.Single(await source.GetCandidatesByArtistsAsync(["seed"]));
        Assert.Null(candidate.UpstreamScore);
    }

    private sealed class RejectOutbound : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Outbound request forbidden.");
    }
}
