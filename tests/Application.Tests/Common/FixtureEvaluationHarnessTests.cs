using System.Globalization;
using LinerNotes.Domain.Tests.Fixtures;
using Xunit;

namespace LinerNotes.Application.Tests.Common;

public sealed class FixtureEvaluationHarnessTests
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>();
    private static IReadOnlyList<EvaluationPick> Identity(IReadOnlyList<EvaluationPick> input) => input;
    private static FixtureEvaluationHarness Harness(string id, IReadOnlyList<FixtureCandidate> input) =>
        new(Phase2ProfileMembership.Locked(), new Dictionary<string, IReadOnlyList<FixtureCandidate>> { [id] = input });

    [Fact]
    public void MembershipIsSealedBeforeRanking()
    {
        var locked = Phase2ProfileMembership.Locked();
        Assert.Equal(Phase2ProfileMembership.ApprovedHash, locked.Hash);
        Assert.Equal(Phase2ProfileMembership.ReviewReference, locked.ReviewReference);
    }

    [Fact]
    public void EightGenreBenchmarkRunsAreSyntheticShortAndUnpadded()
    {
        var benchmark = BenchmarkSeedFixtures.AllCandidates;
        Assert.Equal(16, benchmark.Count);
        for (int g = 0; g < ProfileLock.Genres.Length; g++)
        {
            var id = "synthetic-" + ProfileLock.Genres[g].ToLowerInvariant();
            // Precomputed fake explanations exercise mechanics, not a formula.
            var input = benchmark.Skip(2 * g).Take(2).Select(c => new FixtureCandidate(
                EvaluationMetricsTests.Pick(c.CandidateKey, c.Track.NormalizedArtistName))).ToArray();
            var report = Harness(id, input).Run(id, None, None, Identity);
            Assert.Equal(EvaluationMetrics.Label, report.Label);
            Assert.Equal(2, report.TopThree.Count);
            Assert.Equal(2, report.TopFive.Count);
            Assert.Single(report.Gaps);
            Assert.All(report.TopFive, p => Assert.True(EvaluationMetrics.ExplanationValid(p)));
        }
    }

    [Fact]
    public void ExactKnownAndDislikedTracksAreExcludedAndSiblingsRemainEligible()
    {
        var input = new[] { "artist:known", "artist:disliked", "artist:sibling", "other:track" }
            .Select(key => new FixtureCandidate(EvaluationMetricsTests.Pick(key))).ToArray();
        var report = Harness("synthetic-exclusion", input).Run("synthetic-exclusion",
            new HashSet<string> { "ARTIST:KNOWN" }, new HashSet<string> { "artist:disliked" }, Identity);
        Assert.Equal(new[] { "artist:sibling", "other:track" }, report.TopFive.Select(p => p.Key));
    }

    [Fact]
    public void RealHeldOutUnknownAndMixedInputsFailBeforeCallback()
    {
        int callbacks = 0;
        IReadOnlyList<EvaluationPick> Callback(IReadOnlyList<EvaluationPick> input) { callbacks++; return input; }
        var harness = Harness("synthetic-metal", [new(EvaluationMetricsTests.Pick("a"), "live")]);
        foreach (var id in new[] { "founder-1", "development-metal-1", "heldout-metal-1", "unknown", "synthetic-metal" })
            Assert.Throws<InvalidOperationException>(() => harness.Run(id, None, None, Callback));
        Assert.Equal(0, callbacks);
        var gap = Harness("synthetic-metal", []).Run("synthetic-missing", None, None, Callback);
        Assert.Empty(gap.TopFive);
        Assert.Single(gap.Gaps);
        Assert.Equal(0, callbacks);
    }

    [Fact]
    public void InvalidDuplicateFabricatedAndNonfiniteBreakdownsFail()
    {
        var valid = EvaluationMetricsTests.Pick("a");
        Assert.Throws<ArgumentException>(() => Harness("synthetic-invalid", [new(valid with { Score = double.NaN })])
            .Run("synthetic-invalid", None, None, Identity));
        Assert.Throws<ArgumentException>(() => Harness("synthetic-invalid", [new(valid), new(valid)])
            .Run("synthetic-invalid", None, None, Identity));
        var harness = Harness("synthetic-invalid", [new(valid)]);
        Assert.Throws<InvalidOperationException>(() => harness.Run("synthetic-invalid", None, None, _ => []));
        Assert.Throws<InvalidOperationException>(() => harness.Run("synthetic-invalid", None, None, _ => [valid with { Key = "fabricated" }]));
        Assert.Throws<InvalidOperationException>(() => harness.Run("synthetic-invalid", None, None, _ => [valid with { Score = double.PositiveInfinity }]));
    }

    [Fact]
    public void SnapshotAndCancellationPreventMutationOrPartialResults()
    {
        var contributions = new List<double> { .25, .75 };
        var source = new List<FixtureCandidate> { new(EvaluationMetricsTests.Pick("a") with { Contributions = contributions }) };
        var harness = Harness("synthetic-empty", source);
        source.Clear(); contributions.Clear();
        Assert.Single(harness.Run("synthetic-empty", None, None, Identity).TopFive);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => harness.Run("synthetic-empty", None, None, Identity, cancelled.Token));
        using var during = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => harness.Run("synthetic-empty", None, None,
            input => { during.Cancel(); return input; }, during.Token));
    }

    [Fact]
    public void TiesAndBreakdownsMatchAcrossRepetitionsPermutationsAndCultures()
    {
        var baseline = Enumerable.Range(0, 7).Select(i => new FixtureCandidate(EvaluationMetricsTests.Pick($"artist:track-{i}"))).ToArray();
        var expected = FixtureEvaluationHarness.CanonicalOutput(Harness("synthetic-tied", baseline).Run("synthetic-tied", None, None, Identity));
        var cultureBefore = CultureInfo.CurrentCulture;
        var uiBefore = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var name in new[] { "", "en-US", "tr-TR", "az-Latn-AZ" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
                var random = new Random(271828);
                for (int n = 0; n < 100; n++)
                {
                    Assert.Equal(expected, FixtureEvaluationHarness.CanonicalOutput(Harness("synthetic-tied", baseline).Run("synthetic-tied", None, None, Identity)));
                    var permuted = baseline.OrderBy(_ => random.Next()).Select(c => c with
                        { Pick = c.Pick with { Contributions = c.Pick.Contributions.Reverse().ToArray() } }).ToArray();
                    var report = Harness("synthetic-tied", permuted).Run("synthetic-tied", None, None, Identity);
                    Assert.Equal(expected, FixtureEvaluationHarness.CanonicalOutput(report));
                    Assert.Equal(new[] { "artist:track-0", "artist:track-1", "artist:track-2", "artist:track-3", "artist:track-4" }, report.TopFive.Select(p => p.Key));
                    var shuffled = Phase2ProfileMembership.Proposed().OrderBy(_ => random.Next()).Select(p => p with { Seeds = p.Seeds.Reverse().ToArray() });
                    Assert.Equal(Phase2ProfileMembership.ApprovedHash, ProfileLock.ComputeHash(shuffled));
                }
            }
        }
        finally { CultureInfo.CurrentCulture = cultureBefore; CultureInfo.CurrentUICulture = uiBefore; }
    }
}
