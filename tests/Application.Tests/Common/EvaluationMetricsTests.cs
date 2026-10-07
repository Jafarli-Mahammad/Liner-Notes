using Xunit;

namespace LinerNotes.Application.Tests.Common;

public sealed class EvaluationMetricsTests
{
    public static EvaluationPick Pick(string key, string artist = "artist", double? listeners = null) =>
        new(key, artist, 1.2, 1, .2, 0, [.25, .75], listeners);

    [Fact]
    public void CoverageAndConcentrationIncludeEmptyAndShortLists()
    {
        IReadOnlyList<EvaluationPick>[] lists = [[], [Pick("a")],
            [Pick("b", "one"), Pick("c", "two"), Pick("d", "three"), Pick("e", "four"), Pick("f", "five")]];
        var result = EvaluationMetrics.Lists(lists, 3);
        Assert.Equal(3, result.Profiles);
        Assert.Equal(1, result.AtLeastThree);
        Assert.Equal(1, result.AtLeastFive);
        Assert.Equal(1d / 3, result.CoverageThree);
        Assert.Equal(7d / 9, result.MacroArtistConcentration, 12);
        Assert.Equal(.25, result.PooledArtistConcentration);
        Assert.Equal(4, result.Picks);
        Assert.Equal(1, result.MeanNovelty);
    }

    [Fact]
    public void OneAdditionalCoverageFailureAmongSixteenBlocksGuardrail()
    {
        var a = new ListMetrics(16, 16, 16, 1, 1, .3, .1, 80, 1);
        Assert.False(EvaluationMetrics.Guardrails(a, a with { AtLeastThree = 15, CoverageThree = 15d / 16 }));
        Assert.False(EvaluationMetrics.Guardrails(a, a with { MacroArtistConcentration = .41 }));
        Assert.False(EvaluationMetrics.Guardrails(a, a with { PooledArtistConcentration = .151 }));
        Assert.True(EvaluationMetrics.Guardrails(a, a with { MacroArtistConcentration = .4, PooledArtistConcentration = .15 }));
    }

    [Fact]
    public void OverlapUsesLongerListAndEmptyIsZero()
    {
        Assert.Equal(0, EvaluationMetrics.Overlap([], [], 5));
        Assert.Equal(.5, EvaluationMetrics.Overlap(["a"], ["a", "b"], 5));
        Assert.Equal(2d / 3, EvaluationMetrics.Overlap(["a", "b", "c"], ["a", "c", "d"], 3));
        Assert.Throws<ArgumentException>(() => EvaluationMetrics.Overlap(["a", "a"], [], 3));
    }

    [Fact]
    public void ExplanationChecksAllContributionsAndNamedComponents()
    {
        var pick = Pick("a");
        Assert.True(EvaluationMetrics.ExplanationValid(pick));
        Assert.False(EvaluationMetrics.ExplanationValid(pick with { Contributions = [.25] }));
        Assert.False(EvaluationMetrics.ExplanationValid(pick with { Score = 1 }));
        Assert.False(EvaluationMetrics.ExplanationValid(pick with { Contributions = [double.NaN] }));
        Assert.False(EvaluationMetrics.ExplanationValid(pick with { Novelty = double.PositiveInfinity }));
        Assert.True(EvaluationMetrics.ExplanationValid(pick with { Score = pick.Score + 1e-11 }));
        Assert.False(EvaluationMetrics.ExplanationValid(pick with { Score = pick.Score + 1e-8 }));
    }

    [Fact]
    public void PopularityMissingnessIsNeverFilledWithZero()
    {
        var pool = Enumerable.Range(0, 10).Select(i => Pick(i.ToString(), listeners: i < 8 ? i : null)).ToArray();
        var result = EvaluationMetrics.Popularity(pool, [pool[0], pool[9]]);
        Assert.Equal(.8, result.PoolKnownFraction);
        Assert.Equal(.5, result.SelectedKnownFraction);
        Assert.False(result.Assessable);
        Assert.Null(result.HighestDecileShare);
        Assert.Null(result.MedianPercentile);
        Assert.False(EvaluationMetrics.Popularity(pool, []).Assessable);
    }

    [Fact]
    public void PopularityCollapseIsADiagnosticUsingPoolMidranks()
    {
        var pool = Enumerable.Range(0, 10).Select(i => Pick(i.ToString(), listeners: i)).ToArray();
        var a = EvaluationMetrics.Popularity(pool, pool.Take(3).ToArray());
        var b = EvaluationMetrics.Popularity(pool, [pool[9]]);
        Assert.True(b.Assessable);
        Assert.Equal(.95, b.MedianPercentile);
        Assert.Equal(1, b.HighestDecileShare);
        Assert.True(EvaluationMetrics.PopularityReviewRequired(a, b));
        var tied = pool.Select(p => p with { Listeners = 1 }).ToArray();
        Assert.Equal(.5, EvaluationMetrics.Popularity(tied, tied).MedianPercentile);
        Assert.Equal(0, EvaluationMetrics.Popularity(tied, tied).HighestDecileShare);
        Assert.Throws<ArgumentException>(() => EvaluationMetrics.Popularity(pool, [Pick("outside")]));
    }

    [Fact]
    public void BlindRatingsRequireEveryAvailableTrackAndUseFiveSlots()
    {
        Assert.Equal(.2, EvaluationMetrics.BlindRating(["a", "b"], new Dictionary<string, int> { ["a"] = 1, ["b"] = 0 }));
        Assert.Equal(0, EvaluationMetrics.BlindRating([], new Dictionary<string, int>()));
        Assert.Throws<ArgumentException>(() => EvaluationMetrics.BlindRating(["a"], new Dictionary<string, int>()));
        Assert.Throws<ArgumentException>(() => EvaluationMetrics.BlindRating(["a"], new Dictionary<string, int> { ["a"] = 2 }));
        Assert.True(EvaluationMetrics.BlindMargin([0, 0, 0, 0, 0, 0], [.4, .4, .4, .4, -.2, -.2]));
        Assert.False(EvaluationMetrics.BlindMargin([0, 0, 0], [1, 0, 0]));
        Assert.False(EvaluationMetrics.BlindMargin([0, 0, 0], [.2, .2, 0]));
        Assert.True(EvaluationMetrics.BlindMargin([0, 0, 0], [.4, .2, 0]));
    }

    [Theory]
    [InlineData(.05, .8)]
    [InlineData(.1, .7)]
    public void PerturbationsKeepZerosAndExerciseIndividualAndJointExtremes(double fraction, double threshold)
    {
        var variations = EvaluationMetrics.Perturbations([1, .2, 0, 0], fraction);
        Assert.Equal(8, variations.Count);
        Assert.All(variations, w => { Assert.Equal(0, w[2]); Assert.Equal(0, w[3]); });
        Assert.Contains(variations, w => w[0] == 1 * (1 + fraction) && w[1] == .2 * (1 - fraction));
        Assert.True(EvaluationMetrics.StabilityPass(threshold, threshold + .05, fraction));
        Assert.False(EvaluationMetrics.StabilityPass(threshold - .01, threshold, fraction));
        Assert.False(EvaluationMetrics.StabilityPass(threshold, threshold + .06, fraction));
    }

    [Fact]
    public void SeedYieldRetainsGapsAndDeduplicates()
    {
        var result = EvaluationMetrics.SeedYield(new Dictionary<string, IReadOnlyList<string>> { ["missing"] = [], ["seed"] = ["a", "a", "b"] });
        Assert.Equal(0, result["missing"]);
        Assert.Equal(2, result["seed"]);
    }
}
