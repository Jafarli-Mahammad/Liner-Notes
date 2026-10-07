using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;

namespace LinerNotes.Application.Tests.Common;

// Evaluation only. Production RecommendationScorer and persisted ScoreBreakdown are unchanged.
public sealed record PilotWeights(double Tag, double Novelty, double Match);
public sealed record PilotContribution(string Tag, double UserWeight, double CandidateWeight, double Idf, double Value);
public sealed record PilotScore(string Formula, string Key, string Title, string Artist, double Score,
    double TagScore, double Novelty, double MatchBonus, double Familiarity, double? Listeners,
    string? WinningSeed, double? AggregatedMatch, DiscoveryPath? WinningMatchPath,
    string MatchRule, string? TagMissingReason, ImmutableArray<PilotContribution> Contributions,
    [property: JsonIgnore] HydratedCandidateTrack Evidence)
{
    public EvaluationPick MetricPick() => new(Key, ProfileLock.Canonical(Artist), Score, TagScore,
        Novelty, MatchBonus, Contributions.Select(c => c.Value).ToArray(), Listeners, Familiarity);
}
public sealed record PilotArtist(string Identity, WeightedTagVector Vector,
    [property: JsonIgnore] ImmutableArray<TagObservation> RawTags, ResponseReference Response);
public sealed record PilotIdf(int Artists, ImmutableSortedDictionary<string, int> DocumentFrequency,
    ImmutableSortedDictionary<string, double> Factors, string CorpusHash);

public static class PilotScoring
{
    public const string Version = "pilot-ad-v1";
    public const string MatchRule = "max-valid-match-v1";
    public const string Definition = "A=sum-seed-vectors cosine; B=best-seed cosine (canonical seed ties); " +
        "C=B+max-valid-match-v1 over profile paths (seed/method/hash ties); D=IDF cosine on A; " +
        "idf=1+ln((artists+1)/(df+1)), unseen=1, apply sqrt(idf) to both vectors; " +
        "all normalized overlapping contributions retained, empty denominator=0 with reason; " +
        "novelty=1-declared-artist-familiarity, seed artists=1, other artists=0; " +
        "known/disliked track exclusions from declared sets, default empty artist-only profiles; " +
        "scores descending then canonical track key ordinal; no padding; popularity diagnostic only.";

    public static PilotIdf FitIdf(IEnumerable<PilotArtist> corpus)
    {
        var artists = corpus.OrderBy(a => ProfileLock.Canonical(a.Identity), StringComparer.Ordinal).ToArray();
        if (artists.Select(a => ProfileLock.Canonical(a.Identity)).Distinct(StringComparer.Ordinal).Count() != artists.Length)
            throw new ArgumentException("IDF counts each distinct artist once.");
        foreach (var artist in artists) Validate(artist.Vector);
        var df = artists.SelectMany(a => a.Vector.Weights.Keys).GroupBy(t => t, StringComparer.Ordinal)
            .ToImmutableSortedDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var factors = df.ToImmutableSortedDictionary(p => p.Key,
            p => 1 + Math.Log((artists.Length + 1d) / (p.Value + 1d)), StringComparer.Ordinal);
        var canonical = artists.Select(a => new { identity = ProfileLock.Canonical(a.Identity),
            hash = a.Response.ResponseSha256, tags = a.Vector.Weights.OrderBy(p => p.Key, StringComparer.Ordinal).ToArray() });
        return new(artists.Length, df, factors, Hash(JsonSerializer.Serialize(canonical)));
    }

    public static ImmutableArray<PilotScore> Rank(string formula, EvaluationProfile profile,
        IReadOnlyDictionary<string, WeightedTagVector> seeds, IEnumerable<HydratedCandidateTrack> candidates,
        PilotIdf idf, PilotWeights weights, IReadOnlySet<string> known, IReadOnlySet<string> disliked)
    {
        if (formula is not ("A" or "B" or "C" or "D") || profile.Set is EvaluationSet.HeldOut)
            throw new ArgumentException("Pilot cannot evaluate held-out profiles or unknown formulas.");
        if (new[] { weights.Tag, weights.Novelty, weights.Match }.Any(w => !double.IsFinite(w) || w < 0) ||
            (formula != "C" && weights.Match != 0)) throw new ArgumentException("Invalid evaluation coefficients.");
        var seedKeys = profile.Seeds.Select(ProfileLock.Canonical).Order(StringComparer.Ordinal).ToArray();
        if (seedKeys.Any(s => !seeds.ContainsKey(s))) throw new ArgumentException("Missing seed snapshot, including empty ones.");
        var vectors = seedKeys.Select(s => (Key: s, Vector: seeds[s])).ToArray();
        foreach (var seed in vectors) Validate(seed.Vector);
        var aggregate = Aggregate(vectors.Select(s => s.Vector));
        var excluded = known.Concat(disliked).Select(ProfileLock.Canonical).ToHashSet(StringComparer.Ordinal);
        var eligible = candidates.Where(c => !excluded.Contains(ProfileLock.Canonical(c.RawCandidate.CandidateKey)))
            .OrderBy(c => c.RawCandidate.CandidateKey, StringComparer.Ordinal).ToArray();
        if (eligible.Select(c => c.RawCandidate.CandidateKey).Distinct(StringComparer.Ordinal).Count() != eligible.Length)
            throw new ArgumentException("Duplicate candidate identities.");
        return eligible.Select(candidate =>
        {
            Validate(candidate.TagVector);
            string? winner = null;
            var left = aggregate;
            if (formula is "B" or "C")
            {
                var best = vectors.Select(s => (s.Key, s.Vector, Value: Cosine(s.Vector, candidate.TagVector, null, 1).Sum(c => c.Value)))
                    .OrderByDescending(s => s.Value).ThenBy(s => s.Key, StringComparer.Ordinal).FirstOrDefault();
                winner = best.Key; left = best.Vector ?? WeightedTagVector.Empty;
            }
            var contributions = Cosine(left, candidate.TagVector, formula == "D" ? idf : null, weights.Tag);
            var matchPaths = candidate.RawCandidate.Paths.Where(p => seedKeys.Contains(ProfileLock.Canonical(p.Seed), StringComparer.Ordinal) &&
                p.SimilarityResponse is { Provider: "Last.fm", Method: "artist.getsimilar" }).ToArray();
            if (matchPaths.Any(p => p.Match.IsValid && (!double.IsFinite(p.Match.Value!.Value) || p.Match.Value is < 0 or > 1)))
                throw new ArgumentException("Invalid match was presented as valid.");
            var winningPath = matchPaths.Where(p => p.Match.IsValid)
                .OrderByDescending(p => p.Match.Value).ThenBy(p => ProfileLock.Canonical(p.Seed), StringComparer.Ordinal)
                .ThenBy(p => p.SimilarityResponse!.Method, StringComparer.Ordinal)
                .ThenBy(p => p.SimilarityResponse!.ResponseSha256, StringComparer.Ordinal)
                .ThenBy(p => p.TrackResponse.ResponseSha256, StringComparer.Ordinal).FirstOrDefault();
            double? match = winningPath?.Match.Value;
            double familiarity = seedKeys.Contains(ProfileLock.Canonical(candidate.RawCandidate.ArtistName), StringComparer.Ordinal) ? 1 : 0;
            double tagScore = contributions.Sum(c => c.Value), novelty = weights.Novelty * (1 - familiarity);
            double bonus = formula == "C" ? weights.Match * (match ?? 0) : 0;
            string? missing = left.IsEmpty ? "missing_seed_tag_vector" : candidate.TagVector.IsEmpty ? "missing_candidate_tag_vector" : null;
            var score = new PilotScore(formula, candidate.RawCandidate.CandidateKey, candidate.RawCandidate.Title,
                candidate.RawCandidate.ArtistName, tagScore + novelty + bonus, tagScore, novelty, bonus, familiarity,
                ComparableListeners(candidate), winner, formula == "C" ? match : null,
                formula == "C" ? winningPath : null, formula == "C" ? MatchRule : "not_applicable", missing, contributions, candidate);
            if (!EvaluationMetrics.ExplanationValid(score.MetricPick())) throw new ArgumentException("Nonfinite or unreconciled score.");
            return score;
        }).OrderByDescending(c => c.Score).ThenBy(c => c.Key, StringComparer.Ordinal).ToImmutableArray();
    }

    public static double? ComparableListeners(HydratedCandidateTrack candidate)
    {
        // Repeated paths to the same observation do not fill missing or conflicting counts.
        var observations = candidate.RawCandidate.Paths.GroupBy(p => (p.TrackResponse.ResponseSha256, p.UpstreamPosition))
            .Select(g => g.First().TrackListeners).ToArray();
        if (observations.Any(o => !o.IsValid || o.Value < 0 || o.Value > 9_007_199_254_740_992L)) return null;
        var values = observations.Select(o => o.Value!.Value).Distinct().ToArray();
        return values.Length == 1 ? values[0] : null;
    }

    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void Validate(WeightedTagVector vector)
    {
        if (vector.Weights.Any(p => !double.IsFinite(p.Value) || p.Value <= 0))
            throw new ArgumentException("Nonfinite tag evidence.");
    }
    private static WeightedTagVector Aggregate(IEnumerable<WeightedTagVector> vectors)
    {
        var weights = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (var vector in vectors)
            foreach (var p in vector.Weights.OrderBy(p => p.Key, StringComparer.Ordinal))
                weights[p.Key] = weights.GetValueOrDefault(p.Key) + p.Value;
        var result = WeightedTagVector.FromDictionary(weights); Validate(result); return result;
    }
    private static ImmutableArray<PilotContribution> Cosine(WeightedTagVector user, WeightedTagVector candidate,
        PilotIdf? idf, double weight)
    {
        double Factor(string tag) => idf?.Factors.GetValueOrDefault(tag, 1) ?? 1;
        double Magnitude(WeightedTagVector vector) => Math.Sqrt(vector.Weights.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Sum(p => Factor(p.Key) * p.Value * p.Value));
        double denominator = Magnitude(user) * Magnitude(candidate);
        if (!double.IsFinite(denominator)) throw new ArgumentException("Overflowing tag evidence.");
        if (denominator == 0) return [];
        return candidate.Weights.OrderBy(p => p.Key, StringComparer.Ordinal).Where(p => user[p.Key] > 0)
            .Select(p => new PilotContribution(p.Key, user[p.Key], p.Value, Factor(p.Key),
                weight * Factor(p.Key) * p.Value * user[p.Key] / denominator)).ToImmutableArray();
    }
}
