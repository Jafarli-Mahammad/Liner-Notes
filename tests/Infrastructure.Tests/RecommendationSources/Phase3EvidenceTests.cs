using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;
using LinerNotes.Infrastructure.Tests.Fakes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace LinerNotes.Infrastructure.Tests.RecommendationSources;

public sealed class Phase3EvidenceTests
{
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);
    private static RecordedLastFmResponse Recording(string method, string seed, string json, int limit = int.MaxValue)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return new(method, seed, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)), RecordedAt, limit);
    }

    [Theory]
    [InlineData(null, null, "missing")]
    [InlineData("", null, "malformed")]
    [InlineData("0", 0d, null)]
    [InlineData("1", 1d, null)]
    [InlineData("0.75", .75, null)]
    [InlineData("-0.1", null, "out_of_range")]
    [InlineData("1.1", null, "out_of_range")]
    [InlineData("NaN", null, "nonfinite")]
    [InlineData("Infinity", null, "nonfinite")]
    [InlineData("1e309", null, "nonfinite")]
    [InlineData("0,75", null, "malformed")]
    public void MatchPreservesRawMissingAndInvalidValuesWithoutClamping(string? raw, double? value, string? reason)
    {
        var parsed = LastFmEvidenceParser.Match(raw);
        Assert.Equal(value, parsed.Value);
        Assert.Equal(raw, parsed.RawValue);
        Assert.Equal(reason, parsed.MissingReason);
    }

    [Theory]
    [InlineData(null, null, "missing")]
    [InlineData("0", 0L, null)]
    [InlineData("9007199254740993", 9007199254740993L, null)]
    [InlineData("-1", null, "out_of_range")]
    [InlineData("NaN", null, "malformed_or_overflow")]
    [InlineData("9223372036854775808", null, "malformed_or_overflow")]
    public void CountsRetainScopeAndExactIntegerEvidence(string? raw, long? value, string? reason)
    {
        var parsed = LastFmEvidenceParser.Count(raw);
        Assert.Equal(raw, parsed.RawValue); Assert.Equal(value, parsed.Value); Assert.Equal(reason, parsed.MissingReason);
    }

    [Fact]
    public async Task ReplayUsesOriginalHashTimestampAndNullableTagCountsWithoutNetwork()
    {
        const string json = """{"toptags":{"tag":[{"name":"missing"},{"name":"invalid","count":"not-a-count"},{"name":"negative","count":-1},{"name":"valid","count":"25"}]}}""";
        var original = Recording("artist.getTopTags", "artist", json);
        var api = new RecordedLastFmApiClient([original]);
        var response = await api.GetArtistTopTagsAsync("artist", 15);
        Assert.Equal(EvidenceOrigin.Recorded, response.Reference.Origin);
        Assert.Equal(original.Sha256, response.Reference.ResponseSha256);
        Assert.Equal(RecordedAt, response.Reference.RetrievedAtUtc);
        Assert.Null(response[0].Count); Assert.Null(response[0].RawCount);
        Assert.Null(response[1].Count); Assert.Equal("not-a-count", response[1].RawCount);
        Assert.Null(response[2].Count); Assert.Equal("-1", response[2].RawCount);
        Assert.Equal(25, response[3].Count);
        Assert.Contains((await api.GetArtistTopTagsAsync("unknown", 15)).Gaps, gap => gap.Reason == "recording_request_not_available");
    }

    [Fact]
    public async Task ReplayVerifiesHashCopiesPayloadAndPreservesRepeatedObservations()
    {
        var bytes = Encoding.UTF8.GetBytes("""{"similarartists":{"artist":{"name":"artist","match":"0.5"}}}""");
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Assert.Throws<ArgumentException>(() => new RecordedLastFmResponse("artist.getsimilar", "seed", bytes, new string('0', 64), RecordedAt));
        var first = new RecordedLastFmResponse("artist.getsimilar", "seed", bytes, hash, RecordedAt, 5);
        bytes[0] = 0;
        var second = Recording("artist.getsimilar", "seed", """{"similarartists":{"artist":[{"name":"artist","match":"0.7"}]}}""", 5);
        var client = new RecordedLastFmApiClient([first, second]);
        var tooLarge = await client.GetSimilarArtistsAsync("seed", 6);
        Assert.Contains(tooLarge.Gaps, gap => gap.Reason == "recorded_limit_insufficient");
        var a = await client.GetSimilarArtistsAsync("seed", 5);
        var b = await client.GetSimilarArtistsAsync("seed", 5);
        Assert.Equal("0.5", Assert.Single(a).Match);
        Assert.Equal("0.7", Assert.Single(b).Match);
        Assert.NotEqual(a.Reference.ResponseSha256, b.Reference.ResponseSha256);
        Assert.Empty(await client.GetSimilarArtistsAsync("seed", 5));
    }

    [Fact]
    public async Task DiscoveryRetainsAllSeedsRepeatedPathsRawMatchAndTrackAttribution()
    {
        var api = Substitute.For<ILastFmApiClient>();
        var a = EvidenceFixtures.Response([new LastFmArtistSummary("artist", null, "0.9", "https://www.last.fm/music/artist")], "seed1");
        var b = EvidenceFixtures.Response([new LastFmArtistSummary("artist", null, "bad", "https://www.last.fm/music/artist")], "seed2");
        api.GetSimilarArtistsAsync("seed1", 10, Arg.Any<CancellationToken>()).Returns(a);
        api.GetSimilarArtistsAsync("seed2", 10, Arg.Any<CancellationToken>()).Returns(b);
        api.GetArtistTopTracksAsync("artist", 2, Arg.Any<CancellationToken>()).Returns(EvidenceFixtures.Response(
            [new LastFmTrackItem("track", null, "https://www.last.fm/music/artist/_/track", null, "12", null, new("artist", null, null), "1")], "artist"));
        var source = new LastFmRecommendationSource(api, NullLogger<LastFmRecommendationSource>.Instance);
        var candidate = Assert.Single(await source.GetCandidatesByArtistsAsync(["seed2", "seed1", "seed2"]));
        Assert.Equal(3, candidate.Paths.Length);
        Assert.Equal(new[] { "seed1", "seed2", "seed2" }, candidate.Paths.Select(p => p.Seed));
        Assert.Equal(.9, candidate.Paths[0].Match.Value);
        Assert.All(candidate.Paths.Skip(1), path => { Assert.Null(path.Match.Value); Assert.Equal("bad", path.Match.RawValue); });
        Assert.Null(candidate.UpstreamScore); // No silent max/first-path aggregation policy.
        Assert.All(candidate.Paths, path =>
        {
            Assert.Equal(12, path.TrackListeners.Value);
            Assert.Equal("https://www.last.fm/music/artist/_/track", path.TrackUrl);
            Assert.Equal("https://www.last.fm/music/artist", path.ArtistUrl);
            Assert.Equal("1", path.RawRank);
            Assert.Equal(1, path.UpstreamPosition);
        });
        await api.Received(1).GetArtistTopTracksAsync("artist", 2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MissingTrackArtistForTagDiscoveryIsAGapRatherThanUnknownArtist()
    {
        var api = new RecordedLastFmApiClient([Recording("tag.gettoptracks", "tag", """{"tracks":{"track":[{"name":"track"}]}}""")]);
        var result = await new LastFmRecommendationSource(api, NullLogger<LastFmRecommendationSource>.Instance).GetCandidatesByTagsAsync(["tag"]);
        Assert.Empty(result);
        Assert.Contains(result.Gaps, g => g.Reason == "missing_track_or_artist_identity");
    }

    [Fact]
    public async Task HydrationRetainsEveryExcludedOrInvalidTagAndUsesOnlySuppliedCounts()
    {
        var api = new RecordedLastFmApiClient([Recording("artist.gettoptags", "artist", """
            {"toptags":{"tag":[{"name":"big","count":200},{"name":"small","count":10},
            {"name":"seen live","count":300},{"name":"missing"},{"name":"bad","count":"NaN"},
            {"name":"zero","count":0},{"name":"negative","count":-5}]}}
            """)]);
        var raw = EvidenceFixtures.Raw("track", "artist", origin: EvidenceOrigin.Recorded);
        var result = Assert.IsType<HydratedCandidateTrack>(await new LastFmCandidateHydrator(api, NullLogger<LastFmCandidateHydrator>.Instance).HydrateCandidateAsync(raw));
        Assert.Equal(7, result.Tags.Length);
        Assert.Equal(1, result.TagVector["big"]);
        Assert.Equal(.05, result.TagVector["small"]); // No fabricated 0.1 floor.
        Assert.Equal(2, result.TagVector.Count);
        Assert.Contains(result.Tags, t => t.Name == "seen live" && t.ExclusionReason == "stoplist");
        Assert.Contains(result.Tags, t => t.Name == "missing" && t.Count.Value is null && t.ExclusionReason == "missing");
        Assert.Contains(result.Tags, t => t.Name == "bad" && t.Count.RawValue == "NaN" && !t.Count.IsValid);
        Assert.Contains(result.Tags, t => t.Name == "negative" && t.Count.RawValue == "-5" && t.Count.Value is null);
        Assert.Null(result.GlobalPopularity);
        Assert.Equal("artist", result.TagScope);
        Assert.Equal("max-observed-descriptive-count-v1", result.TagNormalizationVersion);
    }

    [Fact]
    public async Task MixedOriginsInDiscoveryAndHydrationAreRejected()
    {
        var api = Substitute.For<ILastFmApiClient>();
        api.GetSimilarArtistsAsync("seed", 10, Arg.Any<CancellationToken>()).Returns(EvidenceFixtures.Response([new LastFmArtistSummary("artist", null, ".9", null)], "seed", EvidenceOrigin.Recorded));
        api.GetArtistTopTracksAsync("artist", 2, Arg.Any<CancellationToken>()).Returns(EvidenceFixtures.Response([new LastFmTrackItem("track", null, null, null, null, null, new("artist", null, null))], "artist"));
        var source = new LastFmRecommendationSource(api, NullLogger<LastFmRecommendationSource>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => source.GetCandidatesByArtistsAsync(["seed"]));
        api.GetArtistTopTagsAsync("artist", 15, Arg.Any<CancellationToken>()).Returns(EvidenceFixtures.Response([new LastFmTagItem("tag", 100, null)], "artist", EvidenceOrigin.Recorded));
        var hydrator = new LastFmCandidateHydrator(api, NullLogger<LastFmCandidateHydrator>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => hydrator.HydrateCandidateAsync(EvidenceFixtures.Raw("track", "artist")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => hydrator.HydrateCandidatesBatchAsync(
            [EvidenceFixtures.Raw("track", "artist"), EvidenceFixtures.Raw("other", "artist", origin: EvidenceOrigin.Recorded)]));
    }

    [Fact]
    public async Task MissingCredentialsNeverDispatchHttpOrBecomeFixtures()
    {
        using var handler = new MockHttpMessageHandler(_ => throw new InvalidOperationException("Must not dispatch."));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var api = new LastFmApiClient(http, Options.Create(new LastFmOptions { Mode = LastFmClientMode.Hybrid }), cache, new LastFmRateLimiter(100), NullLogger<LastFmApiClient>.Instance);
        var response = await api.GetSimilarArtistsAsync("Jakuzi");
        Assert.Empty(response); Assert.Empty(handler.RecordedRequests);
        Assert.Equal(EvidenceOrigin.Live, response.Reference.Origin);
        Assert.Null(response.Reference.ResponseSha256); Assert.Null(response.Reference.RetrievedAtUtc);
        Assert.Contains(response.Gaps, gap => gap.Reason == "missing_credentials");
    }

    [Fact]
    public async Task TransportFailureLogsNoCredentialBearingExceptionAndReturnsGap()
    {
        using var handler = new MockHttpMessageHandler(_ => throw new HttpRequestException("https://fake.invalid/?api_key=dummy-secret"));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = new CollectingLogger();
        var api = new LastFmApiClient(http, Options.Create(new LastFmOptions { Mode = LastFmClientMode.Hybrid, ApiKey = "dummy-secret" }), cache, new LastFmRateLimiter(100), logger);
        var response = await api.GetSimilarArtistsAsync("Jakuzi");
        Assert.Empty(response); Assert.Single(handler.RecordedRequests);
        Assert.Contains(response.Gaps, gap => gap.Reason == "transport_error");
        Assert.All(logger.Messages, message => { Assert.DoesNotContain("api_key", message); Assert.DoesNotContain("dummy-secret", message); });
    }

    [Theory]
    [InlineData("", "empty_response")]
    [InlineData("malformed", "malformed_json")]
    [InlineData("shape", "invalid_collection")]
    public async Task RecordedParseFailuresStayRecordedGaps(string kind, string reason)
    {
        string body = kind switch { "" => """{"similarartists":{"artist":[]}}""", "shape" => """{"similarartists":{"artist":false}}""", _ => "invalid-json" };
        var client = new RecordedLastFmApiClient([Recording("artist.getsimilar", "seed", body)]);
        var response = await client.GetSimilarArtistsAsync("seed");
        Assert.Empty(response);
        Assert.Equal(EvidenceOrigin.Recorded, response.Reference.Origin);
        Assert.Contains(response.Gaps, gap => gap.Reason == reason);
    }

    [Fact]
    public async Task CancellationPropagatesAcrossEveryIngestionBoundary()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var api = new RecordedLastFmApiClient([]);
        await Assert.ThrowsAsync<OperationCanceledException>(() => api.GetSimilarArtistsAsync("seed", cancellationToken: cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => new LastFmRecommendationSource(api, NullLogger<LastFmRecommendationSource>.Instance).GetCandidatesByArtistsAsync([], cancellationToken: cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => new LastFmCandidateHydrator(api, NullLogger<LastFmCandidateHydrator>.Instance).HydrateCandidatesBatchAsync([], cancellation.Token));
    }

    [Fact]
    public async Task MalformedSkippedItemsDoNotRenumberOriginalResponsePositions()
    {
        var api = new RecordedLastFmApiClient([
            Recording("artist.getsimilar", "seed", """{"similarartists":{"artist":[{"match":"0.1"},{"name":"artist","match":"0.7"}]}}"""),
            Recording("artist.gettoptracks", "artist", """{"toptracks":{"track":[{}, {"name":"track"}]}}"""),
            Recording("artist.gettoptags", "artist", """{"toptags":{"tag":[{}, {"name":"valid","count":100}]}}""")]);
        var raw = Assert.Single(await new LastFmRecommendationSource(api, NullLogger<LastFmRecommendationSource>.Instance).GetCandidatesByArtistsAsync(["seed"]));
        Assert.Equal(2, raw.Paths[0].UpstreamPosition);
        Assert.Equal(2, raw.Paths[0].SimilarityPosition);
        var hydrated = await new LastFmCandidateHydrator(api, NullLogger<LastFmCandidateHydrator>.Instance).HydrateCandidateAsync(raw);
        Assert.NotNull(hydrated);
        Assert.Collection(hydrated.Tags,
            missing =>
            {
                Assert.Equal(string.Empty, missing.Name);
                Assert.Equal("missing_name", missing.ExclusionReason);
                Assert.Equal(1, missing.UpstreamPosition);
                Assert.False(missing.Count.IsValid);
                Assert.Equal("missing", missing.Count.MissingReason);
            },
            valid =>
            {
                Assert.Equal("valid", valid.Name);
                Assert.Null(valid.ExclusionReason);
                Assert.Equal(2, valid.UpstreamPosition);
                Assert.Equal(100, valid.Count.Value);
            });
        Assert.Contains(hydrated.Gaps, g => g.Reason == "invalid_item_identity");
    }

    [Theory]
    [InlineData(false, false, 0, 1)]
    [InlineData(true, false, 0, 2)]
    [InlineData(false, true, 0, 2)]
    [InlineData(false, false, 120, 2)]
    public async Task CacheReusesOriginalProvenanceOnlyWhenHttpFreshnessAllows(bool noStore, bool noCache, int ageSeconds, int expectedRequests)
    {
        using var handler = new MockHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("""{"similarartists":{"artist":[{"name":"artist","match":".5"}]}}""") };
            response.Headers.CacheControl = new() { MaxAge = TimeSpan.FromSeconds(60), NoStore = noStore, NoCache = noCache };
            response.Headers.Age = TimeSpan.FromSeconds(ageSeconds);
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = new LastFmOptions { Mode = LastFmClientMode.LiveOnly, ApiKey = "dummy-key" };
        var client = new LastFmApiClient(http, Options.Create(options), cache, new LastFmRateLimiter(100), NullLogger<LastFmApiClient>.Instance);
        var a = await client.GetSimilarArtistsAsync("seed");
        var b = await client.GetSimilarArtistsAsync("seed");
        Assert.Equal(expectedRequests, handler.RecordedRequests.Count);
        Assert.Equal(a.Reference.ResponseSha256, b.Reference.ResponseSha256);
        if (expectedRequests == 1) Assert.Same(a, b);
        options.Mode = LastFmClientMode.FixtureOnly;
        var synthetic = await client.GetSimilarArtistsAsync("Jakuzi");
        Assert.Equal(EvidenceOrigin.Synthetic, synthetic.Reference.Origin);
        Assert.Equal(expectedRequests, handler.RecordedRequests.Count);
    }

    [Fact]
    public async Task OversizedResponseIsAGapAndCancellationDuringReadPropagates()
    {
        using var handler = MockHttpMessageHandler.WithJsonResponse(new string('x', 100_001));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://ws.audioscrobbler.com/2.0/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var api = new LastFmApiClient(http, Options.Create(new LastFmOptions { Mode = LastFmClientMode.Hybrid, ApiKey = "dummy" }), cache, new LastFmRateLimiter(100), NullLogger<LastFmApiClient>.Instance);
        var response = await api.GetSimilarArtistsAsync("Jakuzi");
        Assert.Empty(response);
        Assert.Contains(response.Gaps, g => g.Reason == "oversized_response");
        Assert.Null(response.Reference.ResponseSha256); // Partial body was not claimed as a complete response.
        using var cancel = new CancellationTokenSource();
        using var cancelling = new MockHttpMessageHandler(_ =>
        {
            cancel.Cancel();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        });
        using var cancellingHttp = new HttpClient(cancelling) { BaseAddress = http.BaseAddress };
        var cancelledApi = new LastFmApiClient(cancellingHttp, Options.Create(new LastFmOptions { ApiKey = "dummy" }), cache, new LastFmRateLimiter(100), NullLogger<LastFmApiClient>.Instance);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledApi.GetSimilarArtistsAsync("seed", cancellationToken: cancel.Token));
    }

    [Fact]
    public async Task SeedOrderAndTagOrderDoNotChangeCanonicalEvidenceOrVectors()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "", "en-US", "tr-TR", "az-Latn-AZ" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var api = Substitute.For<ILastFmApiClient>();
                foreach (var seed in new[] { "first", "second" })
                    api.GetSimilarArtistsAsync(seed, 10, Arg.Any<CancellationToken>()).Returns(EvidenceFixtures.Response([new LastFmArtistSummary("artist", null, ".75", null)], seed));
                api.GetArtistTopTracksAsync("artist", 2, Arg.Any<CancellationToken>()).Returns(EvidenceFixtures.Response([new LastFmTrackItem("track", null, null, null, null, null, new("artist", null, null))], "artist"));
                var source = new LastFmRecommendationSource(api, NullLogger<LastFmRecommendationSource>.Instance);
                var first = Assert.Single(await source.GetCandidatesByArtistsAsync(["first", "second"]));
                var second = Assert.Single(await source.GetCandidatesByArtistsAsync(["second", "first"]));
                Assert.Equal(first.Paths.ToArray(), second.Paths.ToArray());
                var tags = new[] { new LastFmTagItem("big", 100, null), new LastFmTagItem("small", 10, null), new LastFmTagItem("big", 50, null) };
                api.GetArtistTopTagsAsync("artist", 15, Arg.Any<CancellationToken>()).Returns(EvidenceFixtures.Response(tags, "artist"));
                var hydrator = new LastFmCandidateHydrator(api, NullLogger<LastFmCandidateHydrator>.Instance);
                var original = await hydrator.HydrateCandidateAsync(first);
                api.GetArtistTopTagsAsync("artist", 15, Arg.Any<CancellationToken>()).Returns(EvidenceFixtures.Response(tags.Reverse(), "artist"));
                var permuted = await hydrator.HydrateCandidateAsync(second);
                Assert.NotNull(original); Assert.NotNull(permuted);
                Assert.Equal(original.TagVector.Weights.OrderBy(p => p.Key, StringComparer.Ordinal), permuted.TagVector.Weights.OrderBy(p => p.Key, StringComparer.Ordinal));
                Assert.Equal(3, permuted.Tags.Length);
            }
        }
        finally { CultureInfo.CurrentCulture = before; }
    }

    private sealed class CollectingLogger : ILogger<LastFmApiClient>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
