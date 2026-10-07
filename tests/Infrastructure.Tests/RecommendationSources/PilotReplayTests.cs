using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Tests.Common;
using LinerNotes.Infrastructure.RecommendationSources.LastFm;
using Xunit;

namespace LinerNotes.Infrastructure.Tests.RecommendationSources;

public sealed class PilotReplayTests
{
    private static PilotReplayInput Fixture(bool complete = true)
    {
        var seeds = Phase2ProfileMembership.Locked().Profiles.Where(p => p.Set == EvaluationSet.Founder)
            .SelectMany(p => p.Seeds.Select(s => new PilotSeed("founder", p.Genre, s)))
            .Concat(new[] { "Agalloch", "Pharoah Sanders", "Altın Gün" }.Select(s => new PilotSeed("development", "extra", s))).ToImmutableArray();
        var responses = new List<PilotRecordedInput>();
        void Add(string method, string artist, object data, int limit)
        {
            byte[] body = JsonSerializer.SerializeToUtf8Bytes(data);
            responses.Add(new(method, artist, body, Convert.ToHexStringLower(SHA256.HashData(body)), DateTimeOffset.UnixEpoch, limit));
        }
        var artists = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < seeds.Length; i++)
        {
            string[] similar = Enumerable.Range(0, 5).Select(n => $"fixture artist {i}-{n}").ToArray();
            foreach (var artist in similar) artists.Add(artist);
            artists.Add(seeds[i].Value);
            Add("artist.getSimilar", seeds[i].Value, new { similarartists = new { artist = similar.Select(a => new { name = a, match = "0.7" }) } }, 5);
        }
        foreach (var artist in artists.Order(StringComparer.Ordinal))
        {
            Add("artist.getTopTags", artist, new { toptags = new { tag = new[] { new { name = "rock", count = "100" }, new { name = "electronic", count = "50" } } } }, 15);
            Add("artist.getTopTracks", artist, new { toptracks = new { track = new[] { new { name = "one", listeners = "10" }, new { name = "two", listeners = "20" } } } }, 5);
        }
        return new(PilotProtocol.Initial(), seeds, responses.ToImmutableArray(), responses.Count, complete, [], EvaluationMetrics.Label);
    }

    [Fact]
    public async Task AdequateFabricatedReplayProducesFourExplainedComparisonsWithoutNetwork()
    {
        using var handler = new OutboundRejectingHandler(); using var http = new HttpClient(handler);
        var result = await PilotReplay.BuildAsync(Fixture());
        Assert.Equal(0, handler.Attempts); Assert.Equal(EvaluationMetrics.Label, result.Label);
        Assert.True(result.Shape.CanCompare); Assert.Equal(110, result.Shape.CandidateTracks);
        Assert.Equal(55, result.Shape.CandidateArtists); Assert.Equal(143, result.Requests.Length);
        Assert.Equal(66, result.Evaluation!.Corpus.Artists); Assert.Equal(4, result.Evaluation.Comparisons.Length);
        Assert.All(result.Evaluation.Rankings.Values.SelectMany(p => p.Values).SelectMany(p => p),
            score => Assert.True(EvaluationMetrics.ExplanationValid(score.MetricPick())));
        Assert.DoesNotContain("TrackId", JsonSerializer.Serialize(result));
        Assert.All(result.Candidates, c => Assert.Equal(EvidenceOrigin.Recorded, c.Raw.Origin));
    }

    [Theory]
    [InlineData("yield")]
    [InlineData("tags")]
    [InlineData("noise")]
    [InlineData("integrity")]
    [InlineData("incomplete")]
    public async Task InadequateEvidenceStopsBeforeScoring(string kind)
    {
        var input = Fixture(kind != "incomplete");
        var responses = input.Responses.Select(r =>
        {
            if ((kind == "tags" || kind == "noise") && r.Method == "artist.getTopTags")
            {
                byte[] body = JsonSerializer.SerializeToUtf8Bytes(new { toptags = new { tag = new[] {
                    new { name = kind == "noise" ? "seen live" : "rock", count = "10" } } } });
                return r with { Body = body, Sha256 = Convert.ToHexStringLower(SHA256.HashData(body)) };
            }
            if ((kind == "yield" && r.Method == "artist.getSimilar") || (kind == "integrity" && r.Method == "artist.getTopTracks"))
            {
                byte[] body = "{}"u8.ToArray();
                return r with { Body = body, Sha256 = Convert.ToHexStringLower(SHA256.HashData(body)) };
            }
            return r;
        }).ToImmutableArray();
        var result = await PilotReplay.BuildAsync(input with { Responses = responses });
        Assert.False(result.Shape.CanCompare); Assert.Null(result.Evaluation);
        Assert.Contains(result.Shape.Gates, g => !g.Pass);
    }

    [Fact]
    public async Task MissingMatchesOnlyDisableCAndMissingTrackListenersRemainUnassessable()
    {
        var input = Fixture();
        var responses = input.Responses.Select(r =>
        {
            if (r.Method == "artist.getTopTags") return r;
            using var doc = JsonDocument.Parse(r.Body);
            string raw = System.Text.Encoding.UTF8.GetString(r.Body).Replace(",\"match\":\"0.7\"", "", StringComparison.Ordinal)
                .Replace(",\"listeners\":\"10\"", "", StringComparison.Ordinal).Replace(",\"listeners\":\"20\"", "", StringComparison.Ordinal);
            byte[] body = System.Text.Encoding.UTF8.GetBytes(raw);
            return r with { Body = body, Sha256 = Convert.ToHexStringLower(SHA256.HashData(body)) };
        }).ToImmutableArray();
        var result = await PilotReplay.BuildAsync(input with { Responses = responses });
        Assert.True(result.Shape.CanCompare); Assert.False(result.Shape.MatchAssessable); Assert.Equal(0, result.Shape.ListenersKnown);
        Assert.False(result.Evaluation!.Rankings.ContainsKey("C"));
        Assert.All(result.Evaluation.Comparisons, c => { Assert.Null(c.PopularityReviewAgainstA); Assert.All(c.Popularity.Values, p => Assert.False(p.Assessable)); });
    }

    [Fact]
    public async Task TamperedBytesHeldOutSeedsAndChangedWeightsFailClosed()
    {
        var input = Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() => PilotReplay.BuildAsync(input with { Responses = input.Responses.SetItem(0, input.Responses[0] with { Sha256 = new string('f', 64) }) }));
        await Assert.ThrowsAsync<ArgumentException>(() => PilotReplay.BuildAsync(input with { Seeds = input.Seeds.SetItem(0, new("held-out", "extra", "Aphex Twin")) }));
        await Assert.ThrowsAsync<ArgumentException>(() => PilotReplay.BuildAsync(input with { Protocol = input.Protocol with { Weights = input.Protocol.Weights.SetItem("A", new(1, .3, 0)) } }));
    }

    [Fact]
    public async Task CancellationPropagatesAndFullReportIsStableUnderRecordingPermutation()
    {
        var input = Fixture();
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PilotReplay.BuildAsync(input, cancelled.Token));
        var a = await PilotReplay.BuildAsync(input);
        var b = await PilotReplay.BuildAsync(input with { Responses = input.Responses.Reverse().ToImmutableArray(), Seeds = input.Seeds.Reverse().ToImmutableArray() });
        // Ledger/request order is evidence; canonical rankings, corpus and shape are order-independent.
        Assert.Equal(JsonSerializer.Serialize(a.Evaluation), JsonSerializer.Serialize(b.Evaluation));
        Assert.Equal(JsonSerializer.Serialize(a.Shape), JsonSerializer.Serialize(b.Shape));
    }

    [Fact]
    public void StoplistAndNormalizationAreSharedWithProductionHydration()
    {
        Assert.Equal("max-observed-descriptive-count-v1", PilotProtocol.Initial().NormalizationVersion);
        Assert.Equal(12, LastFmTagEvidence.Stoplist.Length);
        Assert.Equal(PilotProtocol.Initial().StoplistHash, PilotScoring.Hash(JsonSerializer.Serialize(LastFmTagEvidence.Stoplist)));
    }

    [Theory]
    [InlineData(11, true)]
    [InlineData(12, false)]
    public async Task TagCoverageIncludesUnusableArtistsAndPassesAtExactlyEightyPercent(int missing, bool expected)
    {
        var input = Fixture();
        int remaining = missing;
        var responses = input.Responses.Select(r =>
        {
            if (r.Method != "artist.getTopTags" || !r.Artist.StartsWith("fixture artist", StringComparison.Ordinal) || remaining-- <= 0) return r;
            string raw = System.Text.Encoding.UTF8.GetString(r.Body).Replace("\"100\"", "\"NaN\"", StringComparison.Ordinal)
                .Replace("\"50\"", "\"NaN\"", StringComparison.Ordinal);
            byte[] body = System.Text.Encoding.UTF8.GetBytes(raw);
            return r with { Body = body, Sha256 = Convert.ToHexStringLower(SHA256.HashData(body)) };
        }).ToImmutableArray();
        var result = await PilotReplay.BuildAsync(input with { Responses = responses }, compare: false);
        var gate = result.Shape.Gates.Single(g => g.Name == "artists-two-usable-tags");
        Assert.Equal(55 - missing, gate.Numerator); Assert.Equal(55, gate.Denominator); Assert.Equal(expected, gate.Pass);
        Assert.Null(result.Evaluation); // Shape stage never invokes A-D even when bars pass.
    }

    [Fact]
    public async Task MissingSnapshotsDoNotClaimAllDeclaredSeedsWereAttempted()
    {
        var input = Fixture();
        var result = await PilotReplay.BuildAsync(input with { Responses = [], Attempts = 1, AcquisitionComplete = false,
            LedgerGaps = [new("Jakuzi", "artist.getsimilar", "response_failed_or_incomplete")] }, compare: false);
        var gate = result.Shape.Gates.Single(g => g.Name == "attempted-all-seeds");
        Assert.Equal(1, gate.Numerator); Assert.Equal(11, gate.Denominator); Assert.False(gate.Pass);
        Assert.Null(result.Evaluation); Assert.Empty(result.Requests);
    }
}
