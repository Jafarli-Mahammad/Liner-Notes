using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Tests.Fixtures;
using Xunit;

namespace LinerNotes.Domain.Tests.Recommendation;

public sealed class BaselineAScorerTests
{
    private readonly BaselineAScorer scorer = new();
    private static readonly IsoWeek Week = new(2026, 41);
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
