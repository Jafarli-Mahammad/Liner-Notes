using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Tests.Fixtures;
using System.Collections;
using System.Text.Json;
using Xunit;

namespace LinerNotes.Domain.Tests.Recommendation;

public sealed class BaselineAScorerTests
{
    private readonly BaselineAScorer scorer = new();
    private static readonly IsoWeek Week = new(2026, 41);
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RankingPreparesConfigurationOncePerBatchAndPreservesSnapshots(bool onsite)
    {
        var policy = new CountingPolicy();
        var configuration = new BaselineAConfiguration(Policy: policy);
        var taste = WeightedTagVector.FromDictionary(Enumerable.Range(0, 271).ToDictionary(i => $"tag-{i:000}", _ => 1d));
        var candidates = Enumerable.Range(0, 100).Select(i => new BaselineACandidate(Track.Create($"Song {i}", $"Artist {i}"),
            WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["tag-000"] = 1 }),
            ScoreEvidence.Empty with { SimilarityInfluence = i % 2 == 0 ? 1 : 0.5 })).ToArray();
        var familiar = new HashSet<string> { "artist 0" };
        var batch = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var result = onsite ? scorer.RankForBatch(candidates, taste, familiar, new HashSet<string>(), batch, configuration)
            : scorer.Rank(candidates, taste, familiar, new HashSet<string>(), Week, configuration);
        Assert.InRange(policy.Enumerations, 1, 2);
        var stable = new BaselineAConfiguration(Policy: new Dictionary<string, string> { ["scope"] = "fixture" });
        foreach (var pick in result)
        {
            var candidate = candidates.Single(c => c.Track.TrackKey == pick.Track.TrackKey);
            var expected = onsite ? scorer.ScoreForBatch(candidate, taste, familiar.Contains(candidate.Track.NormalizedArtistName), batch, stable)
                : scorer.Score(candidate, taste, familiar.Contains(candidate.Track.NormalizedArtistName), Week, stable);
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(pick.Breakdown));
            Assert.Equal(pick.Breakdown.FinalScore, scorer.Replay(pick.Breakdown.Snapshot!).FinalScore);
        }
    }

    private sealed class CountingPolicy : IReadOnlyDictionary<string, string>
    {
        private readonly IReadOnlyDictionary<string, string> values = new Dictionary<string, string> { ["scope"] = "fixture" };
        public int Enumerations { get; private set; }
        public string this[string key] => values[key];
        public IEnumerable<string> Keys => values.Keys;
        public IEnumerable<string> Values => values.Values;
        public int Count => values.Count;
        public bool ContainsKey(string key) => values.ContainsKey(key);
        public bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() { Enumerations++; return values.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void BatchRankingMatchesWeeklyPolicyAndReplaysWithBatchIdentity()
    {
        var candidates = BenchmarkSeedFixtures.AllCandidates.Select(c => new BaselineACandidate(c.Track, c.TagVector, ScoreEvidence.Empty)).ToArray();
        var taste = candidates[0].Tags;
        Guid batch = Guid.NewGuid();
        var weekly = scorer.Rank(candidates, taste, new HashSet<string>(), new HashSet<string>(), Week, new());
        var onsite = scorer.RankForBatch(candidates, taste, new HashSet<string>(), new HashSet<string>(), batch, new());
        Assert.Equal(weekly.Select(p => p.Track.TrackKey), onsite.Select(p => p.Track.TrackKey));
        Assert.All(onsite, p => {
            Assert.Equal(batch, p.Breakdown.Snapshot!.DiscoveryBatchId);
            Assert.Null(p.Breakdown.Snapshot.Week);
            Assert.Equal(p.Breakdown.FinalScore, scorer.Replay(p.Breakdown.Snapshot).FinalScore);
        });
    }
    public static IEnumerable<object[]> Benchmarks => BenchmarkSeedFixtures.AllCandidates.Select(c => new object[] { c });

    [Theory, MemberData(nameof(Benchmarks))]
    public void BenchmarkGenres_ReconstructAllContributionsAndReplay(CandidateTrack fixture)
    {
        var result = scorer.Score(new(fixture.Track, fixture.TagVector, ScoreEvidence.Empty),
            fixture.TagVector, false, Week, new());
        Assert.Equal(1.2, result.FinalScore, 12);
        Assert.Equal(0, result.PopularityPenalty);
        Assert.Equal(0, result.FeedbackPenalty);
        Assert.Equal(result.TagSimilarityScore, result.Snapshot!.Contributions.Sum(c => c.NormalizedWeightedContribution), 12);
        Assert.Equal(result.FinalScore, result.Snapshot.Components.Sum(c => c.Value), 12);
        Assert.Equal(result.FinalScore, scorer.Replay(result.Snapshot).FinalScore);
        Assert.Equal(fixture.TagVector.Count, result.Snapshot.CandidateWeights.Count);
        Assert.DoesNotContain("popularity", result.GenerateExplanation());
        var aggregate = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        { ["rock"] = 0.2, ["ambient"] = 0.3, [fixture.TagVector.Weights.Keys.First()] = 0.7 });
        var different = scorer.Score(new(fixture.Track, fixture.TagVector, ScoreEvidence.Empty), aggregate, true, Week, new());
        Assert.Equal(aggregate.CosineSimilarity(fixture.TagVector), different.FinalScore, 12);
    }

    [Fact]
    public void Rank_UsesCanonicalTiesOneArtistAndBothTrackAliases()
    {
        var tags = WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["rock"] = 1 });
        var picks = scorer.Rank([
            new(Track.Create("B", "Artist"), tags, ScoreEvidence.Empty),
            new(Track.Create("A", "Artist"), tags, ScoreEvidence.Empty),
            new(Track.Create("Known", "Other", mbid: "identifier"), tags, ScoreEvidence.Empty),
            new(Track.Create("Sibling", "Other"), tags, ScoreEvidence.Empty)], tags,
            new HashSet<string>(), new HashSet<string> { "other:known" }, Week, new());
        Assert.Equal(new[] { "A", "Sibling" }, picks.Select(p => p.Track.Title));
    }

    [Fact]
    public void EmptyVectors_StoreReasonsAndFamiliarArtistHasNoBonus()
    {
        var score = scorer.Score(new(Track.Create("A", "Artist"), WeightedTagVector.Empty, ScoreEvidence.Empty),
            WeightedTagVector.Empty, true, Week, new());
        Assert.Equal(0, score.FinalScore);
        Assert.Contains("empty_taste_vector", score.Snapshot!.MissingReasons);
        Assert.Contains("empty_candidate_vector", score.Snapshot.MissingReasons);
        Assert.Empty(score.Snapshot.Contributions);
    }

    [Fact]
    public void StoredReplay_RejectsUnsupportedFormulaAndConfigurationTampering()
    {
        var snapshot = scorer.Score(new(Track.Create("A", "Artist"), WeightedTagVector.Empty, ScoreEvidence.Empty),
            WeightedTagVector.Empty, false, Week, new()).Snapshot!;
        Assert.Throws<NotSupportedException>(() => scorer.Replay(snapshot with { FormulaVersion = "future" }));
        Assert.Throws<ArgumentException>(() => scorer.Replay(snapshot with { Weights = new(2, 0.2) }));
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.MaxValue)]
    public void NonfiniteInputsAndArithmetic_AreRejected(double weight)
    {
        var vector = WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["rock"] = weight });
        Assert.Throws<ArgumentException>(() => scorer.Score(new(Track.Create("A", "Artist"), vector, ScoreEvidence.Empty),
            vector, false, Week, new()));
    }

    [Fact]
    public void ConfigurationHash_UsesOrdinalPolicyOrderAndActualWeights()
    {
        var first = new BaselineAConfiguration(Policy: new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" });
        var second = first with { Policy = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" } };
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.NotEqual(first.Sha256, (first with { NoveltyWeight = 0.3 }).Sha256);
    }
}
