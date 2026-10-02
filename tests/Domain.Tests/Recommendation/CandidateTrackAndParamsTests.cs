using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Scoring;
using Xunit;

namespace LinerNotes.Domain.Tests.Recommendation;

public sealed class CandidateTrackAndParamsTests
{
    [Fact]
    public void CandidateTrack_ValidInputs_InitializesProperly()
    {
        var track = Track.Create("Song X", "Band Y");
        var vector = WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["ambient"] = 0.8 });

        var candidate = new CandidateTrack(track, vector, globalPopularity: 0.25, explicitFamiliarity: 0.5);

        Assert.Equal(track, candidate.Track);
        Assert.Equal(vector, candidate.TagVector);
        Assert.Equal(0.25, candidate.GlobalPopularity);
        Assert.Equal(0.5, candidate.ExplicitFamiliarity);
        Assert.Equal(track.TrackKey, candidate.CandidateKey);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void CandidateTrack_PopularityOutOfRange_ThrowsArgumentOutOfRangeException(double popularity)
    {
        var track = Track.Create("Song", "Band");
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CandidateTrack(track, WeightedTagVector.Empty, popularity));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void CandidateTrack_FamiliarityOutOfRange_ThrowsArgumentOutOfRangeException(double familiarity)
    {
        var track = Track.Create("Song", "Band");
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CandidateTrack(track, WeightedTagVector.Empty, 0.5, explicitFamiliarity: familiarity));
    }

    [Fact]
    public void ScoringParameters_Default_HasExpectedWeights()
    {
        var parameters = ScoringParameters.Default;

        Assert.Equal(1.0, parameters.TagSimilarityWeight);
        Assert.Equal(0.3, parameters.PopularityPenaltyWeight);
        Assert.Equal(0.2, parameters.NoveltyBoostWeight);
        Assert.Equal(1.0, parameters.FeedbackPenaltyWeight);
    }

    [Theory]
    [InlineData(-0.1, 0.3, 0.2, 1.0)]
    [InlineData(1.0, -0.1, 0.2, 1.0)]
    [InlineData(1.0, 0.3, -0.1, 1.0)]
    [InlineData(1.0, 0.3, 0.2, -0.1)]
    public void ScoringParameters_NegativeWeight_ThrowsArgumentOutOfRangeException(
        double tagSim, double popPen, double novBoost, double fbPen)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ScoringParameters(tagSim, popPen, novBoost, fbPen));
    }
}
