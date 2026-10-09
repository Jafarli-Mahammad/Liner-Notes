using System.Collections.Immutable;
using LinerNotes.Application.Common.Models.Recommendation;

namespace LinerNotes.Application.Tests.Common;

public sealed record PilotSeed(string Set, string Cluster, string Value);
public sealed record PilotRequest(ResponseReference Reference, int Items, ImmutableArray<CoverageGap> Gaps,
    int MatchKnown = 0, int MatchTotal = 0);
public sealed record PilotYield(string Seed, int RawPaths, int DistinctTracks, int EligibleTracks, int Artists);
public sealed record PilotClusterYield(string Cluster, int Tracks, int Artists);
public sealed record PilotGate(string Name, int Numerator, int Denominator, double Required, bool Pass);
public sealed record PilotShape(string Label, ImmutableArray<PilotYield> Seeds, ImmutableArray<PilotClusterYield> Clusters,
    ImmutableArray<PilotGate> Gates, bool CanCompare, int CandidateTracks, int CandidateArtists,
    int RawTagEntries, int NoiseTagEntries, int UsableTagEntries, ImmutableArray<string> TagsForAmbiguityReview,
    int ListenersKnown, int ListenersTotal, int MatchKnown, int MatchTotal, bool MatchAssessable,
    string PopularityMeasure, string AmbiguityStatus, ImmutableArray<CoverageGap> Gaps);
public sealed record PilotProfileInput(EvaluationProfile Profile, ImmutableArray<HydratedCandidateTrack> Candidates);
public sealed record PilotStability(string Formula, int K, double Fraction, double WorstOverlap,
    double BaselineWorstOverlap, bool Pass);
public sealed record PilotComparison(string Formula, ListMetrics TopThree, ListMetrics TopFive,
    ImmutableSortedDictionary<string, PopularityMetrics> Popularity, bool? DirectionalGuardrailsAgainstA,
    bool? PopularityReviewAgainstA);
public sealed record PilotEvaluationResult(string Label, string ScoringVersion, PilotIdf Corpus,
    ImmutableSortedDictionary<string, ImmutableSortedDictionary<string, ImmutableArray<PilotScore>>> Rankings,
    ImmutableArray<PilotComparison> Comparisons, ImmutableArray<PilotStability> Stability,
    string Conclusion);

public static class PilotEvaluation
{
    public const string Label = "pilot, directional";
    public const string PopularityMeasure = "artist.getTopTracks track listeners; unique consistent supplied count-v1";

    public static PilotShape Shape(IReadOnlyList<PilotSeed> seeds, IReadOnlyList<PilotArtist> artists,
        IReadOnlyList<HydratedCandidateTrack> candidates, IReadOnlyList<PilotRequest> requests, int attempts,
        IEnumerable<CoverageGap> gaps, IReadOnlySet<string> known, IReadOnlySet<string> disliked, string label,
        IReadOnlySet<string> attemptedSeedKeys)
    {
        var excluded = known.Concat(disliked).Select(ProfileLock.Canonical).ToHashSet(StringComparer.Ordinal);
        var eligible = candidates.Where(c => !excluded.Contains(ProfileLock.Canonical(c.RawCandidate.CandidateKey))).ToArray();
        bool FromSeed(HydratedCandidateTrack c, string seed) => c.RawCandidate.Paths.Any(p =>
            ProfileLock.Canonical(p.Seed) == ProfileLock.Canonical(seed));
        var yield = seeds.OrderBy(s => ProfileLock.Canonical(s.Value), StringComparer.Ordinal).Select(s =>
        {
            var raw = candidates.Where(c => FromSeed(c, s.Value)).ToArray();
            var kept = eligible.Where(c => FromSeed(c, s.Value)).ToArray();
            return new PilotYield(s.Value, raw.Sum(c => c.RawCandidate.Paths.Count(p =>
                ProfileLock.Canonical(p.Seed) == ProfileLock.Canonical(s.Value))), raw.Length, kept.Length,
                kept.Select(c => ProfileLock.Canonical(c.RawCandidate.ArtistName)).Distinct(StringComparer.Ordinal).Count());
        }).ToImmutableArray();
        var clusters = seeds.Where(s => s.Set == "founder").GroupBy(s => s.Cluster, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g =>
            {
                var tracks = eligible.Where(c => g.Any(s => FromSeed(c, s.Value))).ToArray();
                return new PilotClusterYield(g.Key, tracks.Length,
                    tracks.Select(c => ProfileLock.Canonical(c.RawCandidate.ArtistName)).Distinct(StringComparer.Ordinal).Count());
            }).ToImmutableArray();
        var candidateArtists = eligible.Select(c => ProfileLock.Canonical(c.RawCandidate.ArtistName)).Distinct(StringComparer.Ordinal).ToArray();
        var tagCoverage = candidateArtists.Count(a => artists.SingleOrDefault(p => ProfileLock.Canonical(p.Identity) == a)?.Vector.Count >= 2);
        // All recorded artist/tag entries before filtering, once per normalized name per artist.
        var rawTags = artists.SelectMany(a => a.RawTags.Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .GroupBy(t => t.Name.Trim().ToLowerInvariant(), StringComparer.Ordinal).Select(g => new
            { Name = g.Key, Noise = g.Any(t => t.ExclusionReason == "stoplist"), Usable = g.Any(t => t.ExclusionReason is null) })).ToArray();
        int noise = rawTags.Count(t => t.Noise), usableResponses = requests.Count(r => r.Items > 0 && r.Gaps.IsEmpty);
        int matchKnown = requests.Sum(r => r.MatchKnown), matchTotal = requests.Sum(r => r.MatchTotal);
        int listenerKnown = eligible.Count(c => PilotScoring.ComparableListeners(c) is not null);
        int seedsAttempted = seeds.Count(s => attemptedSeedKeys.Contains(ProfileLock.Canonical(s.Value)));
        var gates = new List<PilotGate>
        {
            new("attempted-all-seeds", seedsAttempted, 11, 11, yield.Length == 11 && seedsAttempted == 11),
            new("seeds-five-tracks", yield.Count(s => s.EligibleTracks >= 5), 11, 8, yield.Count(s => s.EligibleTracks >= 5) >= 8),
            new("clusters-ten-tracks-three-artists", clusters.Count(c => c.Tracks >= 10 && c.Artists >= 3), 6, 6,
                clusters.Length == 6 && clusters.All(c => c.Tracks >= 10 && c.Artists >= 3)),
            new("distinct-tracks", eligible.Length, eligible.Length, 60, eligible.Length >= 60),
            new("distinct-artists", candidateArtists.Length, candidateArtists.Length, 20, candidateArtists.Length >= 20),
            new("artists-two-usable-tags", tagCoverage, candidateArtists.Length, .8, Fraction(tagCoverage, candidateArtists.Length) >= .8),
            new("raw-tag-noise", noise, rawTags.Length, .3, rawTags.Length > 0 && Fraction(noise, rawTags.Length) <= .3),
            new("usable-requests", usableResponses, attempts, .95, attempts > 0 && Fraction(usableResponses, attempts) >= .95),
            new("response-integrity", requests.Count(r => r.Reference.ResponseSha256 is { Length: 64 } && r.Reference.RetrievedAtUtc is not null),
                requests.Count, 1, requests.All(r => r.Reference.ResponseSha256 is { Length: 64 } && r.Reference.RetrievedAtUtc is not null))
        };
        return new(label, yield, clusters, gates.ToImmutableArray(), gates.All(g => g.Pass), eligible.Length,
            candidateArtists.Length, rawTags.Length, noise, rawTags.Count(t => t.Usable),
            rawTags.Where(t => !t.Noise).Select(t => t.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray(),
            listenerKnown, eligible.Length, matchKnown, matchTotal, Fraction(matchKnown, matchTotal) >= .8,
            PopularityMeasure, "not verified; non-stoplist tags listed for review, no post-hoc noise classification",
            gaps.OrderBy(g => g.Seed, StringComparer.Ordinal).ThenBy(g => g.Method, StringComparer.Ordinal)
                .ThenBy(g => g.Reason, StringComparer.Ordinal).ThenBy(g => g.Response?.ResponseSha256, StringComparer.Ordinal).ToImmutableArray());
    }

    public static PilotEvaluationResult Compare(PilotShape shape, IReadOnlyList<PilotProfileInput> profiles,
        IReadOnlyList<PilotArtist> artists, IReadOnlyDictionary<string, PilotWeights> weights,
        IReadOnlySet<string> known, IReadOnlySet<string> disliked)
    {
        if (!shape.CanCompare) throw new InvalidOperationException("Yield/tag/integrity gate failed; shape report only.");
        if (profiles.Count != 6 || profiles.Any(p => p.Profile.Set != EvaluationSet.Founder) ||
            profiles.Select(p => p.Profile.Id).Distinct(StringComparer.Ordinal).Count() != 6)
            throw new ArgumentException("Pilot compares the six locked founder profiles only.");
        if (artists.Select(a => a.Response.Origin).Concat(profiles.SelectMany(p => p.Candidates).Select(c => c.Origin)).Distinct().Count() != 1)
            throw new InvalidOperationException("Pilot cannot blend recorded/live/synthetic origins.");
        var idf = PilotScoring.FitIdf(artists.Where(a => a.Response.ResponseSha256 is not null));
        var vectors = artists.ToDictionary(a => ProfileLock.Canonical(a.Identity), a => a.Vector, StringComparer.Ordinal);
        var formulas = new[] { "A", "B", "C", "D" }.Where(f => f != "C" || shape.MatchAssessable).ToArray();
        var rankings = formulas.ToImmutableSortedDictionary(f => f, f => profiles
            .ToImmutableSortedDictionary(p => p.Profile.Id, p => PilotScoring.Rank(f, p.Profile, vectors, p.Candidates, idf, weights[f], known, disliked), StringComparer.Ordinal), StringComparer.Ordinal);
        var comparisons = formulas.Select(f =>
        {
            var lists = rankings[f].Values.Select(l => (IReadOnlyList<EvaluationPick>)l.Select(p => p.MetricPick()).ToArray()).ToArray();
            return new PilotComparison(f, EvaluationMetrics.Lists(lists, 3), EvaluationMetrics.Lists(lists, 5),
                rankings[f].SelectMany(p => new[] { 3, 5 }.Select(k => (Key: $"{p.Key}:k{k}", Value:
                    EvaluationMetrics.Popularity(p.Value.Select(c => c.MetricPick()).ToArray(),
                        p.Value.Take(k).Select(c => c.MetricPick()).ToArray()))))
                    .ToImmutableSortedDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal), null, null);
        }).ToArray();
        var baseline = comparisons.Single(c => c.Formula == "A");
        var stability = new List<PilotStability>();
        foreach (var fraction in new[] { .05, .1 })
        foreach (int k in new[] { 3, 5 })
        {
            var worst = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var f in formulas)
            {
                var w = weights[f];
                worst[f] = EvaluationMetrics.Perturbations([w.Tag, w.Novelty, w.Match], fraction).Min(coefficients =>
                    profiles.Average(p => EvaluationMetrics.Overlap(rankings[f][p.Profile.Id].Select(c => c.Key).ToArray(),
                        PilotScoring.Rank(f, p.Profile, vectors, p.Candidates, idf,
                            new(coefficients[0], coefficients[1], coefficients[2]), known, disliked).Select(c => c.Key).ToArray(), k)));
            }
            foreach (var f in formulas) stability.Add(new(f, k, fraction, worst[f], worst["A"], EvaluationMetrics.StabilityPass(worst[f], worst["A"], fraction)));
        }
        return new(shape.Label, PilotScoring.Version, idf, rankings, comparisons.Select(c => c with
        {
            DirectionalGuardrailsAgainstA = EvaluationMetrics.Guardrails(baseline.TopThree, c.TopThree) && EvaluationMetrics.Guardrails(baseline.TopFive, c.TopFive),
            PopularityReviewAgainstA = c.Popularity.Values.All(p => p.Assessable) && baseline.Popularity.Values.All(p => p.Assessable)
                ? c.Popularity.Any(p => EvaluationMetrics.PopularityReviewRequired(baseline.Popularity[p.Key], p.Value)) : null
        }).ToImmutableArray(), stability.ToImmutableArray(),
            shape.Label + ". No tuning, promotion, production formula choice or held-out inference. Blind review pending; C absent if match evidence is inadequate.");
    }

    private static double Fraction(int n, int d) => d == 0 ? 0 : (double)n / d;
}
