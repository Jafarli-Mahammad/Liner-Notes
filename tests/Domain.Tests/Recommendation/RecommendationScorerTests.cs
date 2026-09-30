using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using LinerNotes.Domain.Tests.Fixtures;
using Xunit;

namespace LinerNotes.Domain.Tests.Recommendation;

public sealed class RecommendationScorerTests
{
    private readonly RecommendationScorer _scorer = new();
    private readonly ScoringParameters _defaultParams = ScoringParameters.Default;

    [Fact]
    public void Score_CloseTasteMatch_ScoresHigherThanDistantMatch()
    {
        // Arrange: User with strong affinity for atmospheric black metal and doom metal
        var userTags = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        {
            ["atmospheric black metal"] = 1.0,
            ["doom metal"] = 0.9,
            ["post-metal"] = 0.8
        });
        var tasteProfile = UserTasteProfile.Create(Guid.NewGuid(), userTags);

        var closeMatch = BenchmarkSeedFixtures.Metal.UndergroundAtmospheric;
        var distantMatch = BenchmarkSeedFixtures.Pop.MainstreamPop;

        // Act
        var closeScore = _scorer.Score(closeMatch, tasteProfile, _defaultParams);
        var distantScore = _scorer.Score(distantMatch, tasteProfile, _defaultParams);

        // Assert
        Assert.True(closeScore.FinalScore > distantScore.FinalScore,
            $"Expected close match ({closeScore.FinalScore:F3}) to score higher than distant match ({distantScore.FinalScore:F3})");
        Assert.True(closeScore.TagSimilarityRaw > 0.5, "Close match should have high raw similarity.");
        Assert.Equal(0.0, distantScore.TagSimilarityRaw);
    }

    [Fact]
    public void Score_PopularityPenalty_PenalizesMainstreamOverUnderground_AllElseEqual()
    {
        // Arrange: Candidate A and B have identical tag overlap, but B is globally popular
        var commonTags = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        {
            ["shoegaze"] = 1.0,
            ["dream pop"] = 0.9
        });
        var userTaste = UserTasteProfile.Create(Guid.NewGuid(), commonTags);

        var undergroundCandidate = new CandidateTrack(
            Track.Create("Underground Dream", "Bedrooms", "Demo"),
            commonTags,
            globalPopularity: 0.10);

        var mainstreamCandidate = new CandidateTrack(
            Track.Create("Mainstream Dream", "Arena Band", "Greatest Hits"),
            commonTags,
            globalPopularity: 0.95);

        // Act
        var undergroundScore = _scorer.Score(undergroundCandidate, userTaste, _defaultParams);
        var mainstreamScore = _scorer.Score(mainstreamCandidate, userTaste, _defaultParams);

        // Assert: Tag similarities are identical
        Assert.Equal(undergroundScore.TagSimilarityRaw, mainstreamScore.TagSimilarityRaw, precision: 6);

        // But popularity penalty reduces mainstream candidate's score
        Assert.True(undergroundScore.PopularityPenalty < mainstreamScore.PopularityPenalty);
        Assert.True(undergroundScore.FinalScore > mainstreamScore.FinalScore,
            $"Underground ({undergroundScore.FinalScore:F3}) should score higher than Mainstream ({mainstreamScore.FinalScore:F3}) due to anti-popularity penalty.");
    }

    [Fact]
    public void Score_FeedbackAdjustment_HeavilyPenalizesRejectedCandidate()
    {
        // Arrange: Candidate strongly matches user tags, but user explicitly rejected this artist
        var tags = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        {
            ["post-punk"] = 1.0,
            ["indie rock"] = 0.8
        });

        var candidate = BenchmarkSeedFixtures.Indie.PostPunkUnderground;

        var normalProfile = UserTasteProfile.Create(Guid.NewGuid(), tags);
        var rejectedProfile = new UserTasteProfile(
            userId: Guid.NewGuid(),
            tagPreferences: tags,
            rejectedArtistNames: new HashSet<string> { candidate.Track.ArtistName });

        // Act
        var normalScore = _scorer.Score(candidate, normalProfile, _defaultParams);
        var rejectedScore = _scorer.Score(candidate, rejectedProfile, _defaultParams);

        // Assert
        Assert.Equal(0.0, normalScore.FeedbackPenalty);
        Assert.True(rejectedScore.FeedbackPenalty > 0, "Rejected profile must incur feedback penalty.");
        Assert.True(normalScore.FinalScore > rejectedScore.FinalScore,
            $"Normal score ({normalScore.FinalScore:F3}) should exceed rejected score ({rejectedScore.FinalScore:F3}).");
    }

    [Fact]
    public void Score_NoveltyAdjustment_BoostsNovelTrackOverFamiliarOne()
    {
        // Arrange: Identical tags and popularity, but one artist is already familiar to user
        var tags = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        {
            ["modal jazz"] = 1.0
        });

        var candidate = BenchmarkSeedFixtures.Jazz.ClassicJazz;

        var novelProfile = UserTasteProfile.Create(Guid.NewGuid(), tags);
        var familiarProfile = new UserTasteProfile(
            userId: Guid.NewGuid(),
            tagPreferences: tags,
            familiarArtistNames: new HashSet<string> { candidate.Track.ArtistName });

        // Act
        var novelScore = _scorer.Score(candidate, novelProfile, _defaultParams);
        var familiarScore = _scorer.Score(candidate, familiarProfile, _defaultParams);

        // Assert
        Assert.True(novelScore.NoveltyBoost > familiarScore.NoveltyBoost,
            $"Novel candidate boost ({novelScore.NoveltyBoost:F3}) should exceed familiar ({familiarScore.NoveltyBoost:F3}).");
        Assert.True(novelScore.FinalScore > familiarScore.FinalScore);
    }

    [Fact]
    public void Score_IsStrictlyDeterministic_1000IterationsProduceIdenticalBits()
    {
        // Arrange
        var userTags = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        {
            ["idm"] = 0.9,
            ["ambient techno"] = 0.8,
            ["post-rock"] = 0.6
        });
        var profile = UserTasteProfile.Create(Guid.NewGuid(), userTags);
        var candidate = BenchmarkSeedFixtures.Electronic.UndergroundIdm;

        // Act: Run baseline
        var baseline = _scorer.Score(candidate, profile, _defaultParams);

        // Run 1,000 times
        for (int i = 0; i < 1000; i++)
        {
            var run = _scorer.Score(candidate, profile, _defaultParams);

            // Assert bitwise identical
            Assert.Equal(baseline.FinalScore, run.FinalScore);
            Assert.Equal(baseline.TagSimilarityRaw, run.TagSimilarityRaw);
            Assert.Equal(baseline.PopularityPenalty, run.PopularityPenalty);
            Assert.Equal(baseline.NoveltyBoost, run.NoveltyBoost);
            Assert.Equal(baseline.FeedbackPenalty, run.FeedbackPenalty);
            Assert.Equal(baseline.GenerateExplanation(), run.GenerateExplanation());
        }
    }

    [Fact]
    public void Rank_BreaksTiesDeterministically()
    {
        // Arrange: Two candidates with identical scores
        var tags = WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["drone"] = 1.0 });
        var profile = UserTasteProfile.Create(Guid.NewGuid(), tags);

        var candidateA = new CandidateTrack(
            Track.Create("Track Beta", "Artist Y", mbid: "mbid:002"),
            tags,
            globalPopularity: 0.1);

        var candidateB = new CandidateTrack(
            Track.Create("Track Alpha", "Artist X", mbid: "mbid:001"),
            tags,
            globalPopularity: 0.1);

        // Act
        var ranked1 = _scorer.Rank(new[] { candidateA, candidateB }, profile, _defaultParams);
        var ranked2 = _scorer.Rank(new[] { candidateB, candidateA }, profile, _defaultParams);

        // Assert: Regardless of input order, ranks are bitwise identical and deterministic
        Assert.Equal(ranked1.Count, ranked2.Count);
        Assert.Equal(ranked1[0].Candidate.CandidateKey, ranked2[0].Candidate.CandidateKey);
        Assert.Equal(ranked1[1].Candidate.CandidateKey, ranked2[1].Candidate.CandidateKey);
        Assert.Equal(1, ranked1[0].Rank);
        Assert.Equal(2, ranked1[1].Rank);
    }

    [Fact]
    public void ScoreBreakdown_GeneratesHonestExplanationFromStoredData()
    {
        // Arrange
        var userTags = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        {
            ["atmospheric black metal"] = 1.0,
            ["doom metal"] = 0.8
        });
        var profile = UserTasteProfile.Create(Guid.NewGuid(), userTags);
        var candidate = BenchmarkSeedFixtures.Metal.UndergroundAtmospheric;

        // Act
        var breakdown = _scorer.Score(candidate, profile, _defaultParams);
        var explanation = breakdown.GenerateExplanation();

        // Assert
        Assert.NotNull(explanation);
        Assert.Contains("Shared tags", explanation);
        Assert.Contains("atmospheric black metal", explanation);
        Assert.Contains("underground discovery", explanation);
        Assert.Contains(breakdown.FinalScore.ToString("F3"), explanation);
    }

    [Fact]
    public void Rank_BenchmarkSeedSet_RanksMetalheadPreferencesCorrectly()
    {
        // Arrange: Metal fan
        var metalheadTags = WeightedTagVector.FromDictionary(new Dictionary<string, double>
        {
            ["atmospheric black metal"] = 1.0,
            ["doom metal"] = 0.9,
            ["post-metal"] = 0.8
        });
        var profile = UserTasteProfile.Create(Guid.NewGuid(), metalheadTags);

        // Act: Rank all 16 candidates from benchmark
        var ranked = _scorer.Rank(BenchmarkSeedFixtures.AllCandidates, profile, _defaultParams);

        // Assert: Agalloch (underground atmospheric metal) should be rank 1
        Assert.Equal(1, ranked[0].Rank);
        Assert.Equal(BenchmarkSeedFixtures.Metal.UndergroundAtmospheric.CandidateKey, ranked[0].Candidate.CandidateKey);

        // Mainstream Pop (Taylor Swift) and Mainstream Rap (Drake) should be near the bottom
        var popRank = ranked.First(r => r.Candidate.CandidateKey == BenchmarkSeedFixtures.Pop.MainstreamPop.CandidateKey).Rank;
        var rapRank = ranked.First(r => r.Candidate.CandidateKey == BenchmarkSeedFixtures.HipHop.MainstreamRap.CandidateKey).Rank;

        Assert.True(popRank > 10, $"Mainstream pop should rank low for metalhead, got rank {popRank}");
        Assert.True(rapRank > 10, $"Mainstream rap should rank low for metalhead, got rank {rapRank}");
    }
}
