namespace LinerNotes.Application.Tests.Common;

// Evaluation mechanics only. No production scoring policy lives in this assembly.
public sealed record EvaluationPick(string Key, string Artist, double Score,
    double TagScore, double Novelty, double Match, IReadOnlyList<double> Contributions,
    double? Listeners = null, double Familiarity = 0);

public sealed record ListMetrics(int Profiles, int AtLeastThree, int AtLeastFive,
    double CoverageThree, double CoverageFive, double MacroArtistConcentration,
    double PooledArtistConcentration, int Picks, double MeanNovelty);

public sealed record PopularityMetrics(int PoolKnown, int PoolTotal, int SelectedKnown,
    int SelectedTotal, double PoolKnownFraction, double SelectedKnownFraction,
    bool Assessable, double? HighestDecileShare, double? MedianPercentile);

public static class EvaluationMetrics
{
    public const string Label = "Mechanics only, no catalog conclusions.";

    public static ListMetrics Lists(IReadOnlyList<IReadOnlyList<EvaluationPick>> lists, int k)
    {
        if (k is not (3 or 5)) throw new ArgumentOutOfRangeException(nameof(k));
        var trimmed = lists.Select(l => l.Take(k).ToArray()).ToArray();
        var picks = trimmed.SelectMany(l => l).ToArray();
        var n = lists.Count;
        int three = lists.Count(l => l.Count >= 3), five = lists.Count(l => l.Count >= 5);
        return new(n, three, five, Fraction(three, n), Fraction(five, n),
            n == 0 ? 1 : trimmed.Average(l => l.Length == 0 ? 1 :
                l.GroupBy(p => p.Artist, StringComparer.Ordinal).Max(g => g.Count()) / (double)l.Length),
            picks.Length == 0 ? 1 : picks.GroupBy(p => p.Artist, StringComparer.Ordinal)
                .Max(g => g.Count()) / (double)picks.Length,
            picks.Length, picks.Length == 0 ? 0 : picks.Average(p => 1 - p.Familiarity));
    }

    public static double Overlap(IReadOnlyList<string> original, IReadOnlyList<string> perturbed, int k)
    {
        if (k <= 0) throw new ArgumentOutOfRangeException(nameof(k));
        var a = original.Take(k).ToArray();
        var b = perturbed.Take(k).ToArray();
        if (a.Distinct(StringComparer.Ordinal).Count() != a.Length ||
            b.Distinct(StringComparer.Ordinal).Count() != b.Length)
            throw new ArgumentException("Duplicate track identities in ranking.");
        return Fraction(a.Intersect(b, StringComparer.Ordinal).Count(), Math.Max(a.Length, b.Length));
    }

    public static bool ExplanationValid(EvaluationPick p)
    {
        var values = p.Contributions.Concat([p.Score, p.TagScore, p.Novelty, p.Match, p.Familiarity]);
        return values.All(double.IsFinite) && p.Familiarity is >= 0 and <= 1 &&
            Near(p.Contributions.Sum(), p.TagScore) && Near(p.TagScore + p.Novelty + p.Match, p.Score);
    }

    public static PopularityMetrics Popularity(IReadOnlyList<EvaluationPick> pool,
        IReadOnlyList<EvaluationPick> selected)
    {
        if (pool.Select(p => p.Key).Distinct(StringComparer.Ordinal).Count() != pool.Count)
            throw new ArgumentException("Eligible pool contains duplicate identities.");
        var byKey = pool.ToDictionary(p => p.Key, StringComparer.Ordinal);
        if (selected.Any(p => !byKey.ContainsKey(p.Key)))
            throw new ArgumentException("Selection is outside its eligible pool.");
        // Reuse the pool measure: selected data cannot replace absent evidence.
        var known = pool.Where(KnownPopularity).ToArray();
        var picks = selected.Select(p => byKey[p.Key]).Where(KnownPopularity).ToArray();
        double poolFraction = Fraction(known.Length, pool.Count), pickFraction = Fraction(picks.Length, selected.Count);
        bool assessable = poolFraction >= .8 && pickFraction >= .8;
        if (!assessable) return new(known.Length, pool.Count, picks.Length, selected.Count,
            poolFraction, pickFraction, false, null, null);
        // Empirical midrank percentile handles ties without ordinal popularity bias.
        double Percentile(double count) => (known.Count(p => p.Listeners < count) +
            .5 * known.Count(p => p.Listeners == count)) / known.Length;
        var percentiles = picks.Select(p => Percentile(p.Listeners!.Value)).Order().ToArray();
        double median = percentiles.Length % 2 == 1 ? percentiles[percentiles.Length / 2] :
            (percentiles[percentiles.Length / 2 - 1] + percentiles[percentiles.Length / 2]) / 2;
        return new(known.Length, pool.Count, picks.Length, selected.Count, poolFraction, pickFraction,
            true, Fraction(percentiles.Count(p => p >= .9), percentiles.Length), median);
    }

    public static bool PopularityReviewRequired(PopularityMetrics a, PopularityMetrics b) =>
        a.Assessable && b.Assessable &&
        ((b.HighestDecileShare > .6 && b.HighestDecileShare - a.HighestDecileShare > .15) ||
         b.MedianPercentile - a.MedianPercentile >= .20);

    public static double BlindRating(IReadOnlyList<string> topFive, IReadOnlyDictionary<string, int> ratings)
    {
        if (topFive.Count > 5 || topFive.Distinct(StringComparer.Ordinal).Count() != topFive.Count)
            throw new ArgumentException("Expected at most five distinct picks.");
        int sum = 0;
        foreach (var key in topFive)
        {
            if (!ratings.TryGetValue(key, out var rating) || rating is < -1 or > 1)
                throw new ArgumentException("Every available track requires a valid blind rating.");
            sum += rating;
        }
        return sum / 5d;
    }

    public static bool BlindMargin(IReadOnlyList<double> baseline, IReadOnlyList<double> challenger)
    {
        if (baseline.Count is not (3 or 6) || baseline.Count != challenger.Count ||
            baseline.Concat(challenger).Any(x => !double.IsFinite(x) || x is < -1 or > 1))
            throw new ArgumentException("Expected complete paired ratings for three or six profiles.");
        var delta = baseline.Zip(challenger, (a, b) => b - a).ToArray();
        return delta.Average() >= .20 - 1e-12 && delta.Count(x => x > 0) >= (baseline.Count == 6 ? 4 : 2);
    }

    public static IReadOnlyList<double[]> Perturbations(IReadOnlyList<double> weights, double fraction)
    {
        if (fraction is not (.05 or .10) || weights.Count > 10 ||
            weights.Any(w => !double.IsFinite(w) || w < 0)) throw new ArgumentException("Invalid perturbation configuration.");
        var positive = Enumerable.Range(0, weights.Count).Where(i => weights[i] > 0).ToArray();
        var result = new List<double[]>();
        foreach (int index in positive)
            foreach (int direction in new[] { -1, 1 })
            {
                var copy = weights.ToArray(); copy[index] *= 1 + direction * fraction; result.Add(copy);
            }
        for (int mask = 0; mask < (1 << positive.Length); mask++)
        {
            if (positive.Length == 0) break;
            var copy = weights.ToArray();
            for (int j = 0; j < positive.Length; j++) copy[positive[j]] *= 1 + ((mask & (1 << j)) == 0 ? -fraction : fraction);
            result.Add(copy);
        }
        return result;
    }

    public static bool Guardrails(ListMetrics a, ListMetrics b) => a.Profiles == b.Profiles &&
        b.CoverageThree >= a.CoverageThree - .05 - 1e-12 &&
        b.MacroArtistConcentration <= a.MacroArtistConcentration + .10 + 1e-12 &&
        b.PooledArtistConcentration <= a.PooledArtistConcentration + .05 + 1e-12;

    public static bool StabilityPass(double worstOverlap, double baselineWorst, double fraction) =>
        double.IsFinite(worstOverlap) && double.IsFinite(baselineWorst) &&
        worstOverlap is >= 0 and <= 1 && baselineWorst is >= 0 and <= 1 &&
        (fraction is .05 or .10) && worstOverlap >= (fraction == .05 ? .8 : .7) - 1e-12 &&
        worstOverlap >= baselineWorst - .05 - 1e-12;

    public static IReadOnlyDictionary<string, int> SeedYield(IReadOnlyDictionary<string, IReadOnlyList<string>> seeds) =>
        seeds.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key,
            p => p.Value.Distinct(StringComparer.Ordinal).Count(), StringComparer.Ordinal);

    private static bool KnownPopularity(EvaluationPick p) => p.Listeners is { } n && double.IsFinite(n) && n >= 0;
    private static bool Near(double a, double b) => Math.Abs(a - b) <= 1e-10 * Math.Max(1, Math.Abs(b));
    private static double Fraction(int count, int total) => total == 0 ? 0 : count / (double)total;
}
