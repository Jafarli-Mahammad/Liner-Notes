using System.Globalization;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;

namespace LinerNotes.Domain.Scoring;

public sealed record BaselineAConfiguration(string Version = "phase6-config-v1", double TagWeight = 1,
    double NoveltyWeight = 0.2, IReadOnlyDictionary<string, string>? Policy = null, string FormulaVersion = "baseline-a-v1")
{
    public string Sha256
    {
        get
        {
            Validate();
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["version"] = Version,
                ["formula"] = FormulaVersion,
                ["tag"] = TagWeight.ToString("R", CultureInfo.InvariantCulture),
                ["novelty"] = NoveltyWeight.ToString("R", CultureInfo.InvariantCulture)
            };
            foreach (var item in Policy ?? new Dictionary<string, string>()) values.Add("policy." + item.Key, item.Value);
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                string.Concat(values.Select(p => $"{p.Key.Length}:{p.Key}{p.Value.Length}:{p.Value}\n")))));
        }
    }

    public void Validate()
    {
        if (FormulaVersion is not ("baseline-a-v1" or "baseline-a-v2") || string.IsNullOrWhiteSpace(Version) || !double.IsFinite(TagWeight) || TagWeight < 0 ||
            !double.IsFinite(NoveltyWeight) || NoveltyWeight < 0)
            throw new ArgumentException("Scoring configuration must have a version and finite nonnegative weights.");
    }
}

public sealed record BaselineACandidate(Track Track, WeightedTagVector Tags, ScoreEvidence Evidence);
public sealed record BaselineAPick(Track Track, ScoreBreakdown Breakdown);

/// <summary>Pure aggregate cosine and binary artist novelty, with stored-input replay.</summary>
public sealed class BaselineAScorer
{
    public ScoreBreakdown Score(BaselineACandidate candidate, WeightedTagVector taste,
        bool familiarArtist, IsoWeek week, BaselineAConfiguration configuration)
        => ScoreCore(candidate, taste, familiarArtist, week.Value, null, configuration);

    public ScoreBreakdown ScoreForBatch(BaselineACandidate candidate, WeightedTagVector taste,
        bool familiarArtist, Guid batchId, BaselineAConfiguration configuration)
    {
        if (batchId == Guid.Empty) throw new ArgumentException("Batch identity required.");
        return ScoreCore(candidate, taste, familiarArtist, null, batchId, configuration);
    }

    public IEnumerable<BaselineAPick> Score(IEnumerable<BaselineACandidate> candidates, WeightedTagVector taste,
        IReadOnlySet<string> familiarArtists, IsoWeek week, BaselineAConfiguration configuration)
        => ScoreMany(candidates, taste, familiarArtists, week.Value, null, configuration);

    public IEnumerable<BaselineAPick> ScoreForBatch(IEnumerable<BaselineACandidate> candidates, WeightedTagVector taste,
        IReadOnlySet<string> familiarArtists, Guid batchId, BaselineAConfiguration configuration)
        => ScoreMany(candidates, taste, familiarArtists, null, batchId, configuration);

    private IEnumerable<BaselineAPick> ScoreMany(IEnumerable<BaselineACandidate> candidates, WeightedTagVector taste,
        IReadOnlySet<string> familiarArtists, string? week, Guid? batchId, BaselineAConfiguration configuration)
    {
        BatchInputs? inputs = null;
        foreach (var candidate in candidates)
        {
            if (batchId == Guid.Empty) throw new ArgumentException("Batch identity required.");
            inputs ??= new(taste, configuration);
            yield return new(candidate.Track, ScoreCore(candidate, taste, familiarArtists.Contains(candidate.Track.NormalizedArtistName),
                week, batchId, configuration, inputs: inputs));
        }
    }

    private sealed class BatchInputs
    {
        public BaselineAConfiguration Configuration { get; }
        public IReadOnlyDictionary<string, double> TasteWeights { get; }
        public double TasteMagnitude { get; }
        private readonly Dictionary<string, string> hashes = new(StringComparer.Ordinal);

        public BatchInputs(WeightedTagVector taste, BaselineAConfiguration configuration)
        {
            configuration.Validate();
            Configuration = configuration with { Policy = configuration.Policy is null ? null :
                new ReadOnlyDictionary<string, string>(configuration.Policy.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal)) };
            TasteWeights = new ReadOnlyDictionary<string, double>(Ordered(taste));
            TasteMagnitude = Math.Sqrt(TasteWeights.Values.Sum(v => v * v));
        }

        public string Hash(BaselineAConfiguration configuration)
        {
            if (!hashes.TryGetValue(configuration.FormulaVersion, out var hash))
                hashes.Add(configuration.FormulaVersion, hash = configuration.Sha256);
            return hash;
        }
    }

    private ScoreBreakdown ScoreCore(BaselineACandidate candidate, WeightedTagVector taste,
        bool familiarArtist, string? week, Guid? batchId, BaselineAConfiguration configuration, double unmatchedTasteSquared = 0, BatchInputs? inputs = null)
    {
        configuration = inputs?.Configuration ?? configuration;
        if (candidate.Evidence.SimilarityInfluence != 1) configuration = configuration with { FormulaVersion = "baseline-a-v2" };
        if (inputs is null) configuration.Validate();
        var tasteWeights = inputs?.TasteWeights ?? Ordered(taste);
        var candidateWeights = Ordered(candidate.Tags);
        if (!double.IsFinite(unmatchedTasteSquared) || unmatchedTasteSquared < 0 || !double.IsFinite(candidate.Evidence.SimilarityInfluence) || candidate.Evidence.SimilarityInfluence is < 0 or > 1) throw new ArgumentException("invalid_stored_influence");
        double tasteMagnitude = inputs?.TasteMagnitude ?? Math.Sqrt(tasteWeights.Values.Sum(v => v * v) + unmatchedTasteSquared), candidateMagnitude = Magnitude(candidateWeights);
        var contributions = candidateWeights.Where(p => tasteWeights.ContainsKey(p.Key)).Select(p =>
            new NormalizedTagContribution(p.Key, p.Value, tasteWeights[p.Key],
                p.Value / candidateMagnitude * (tasteWeights[p.Key] / tasteMagnitude) * configuration.TagWeight * candidate.Evidence.SimilarityInfluence)).ToArray();
        double tagScore = contributions.Sum(p => p.NormalizedWeightedContribution);
        double raw = Cosine(tasteWeights, candidateWeights, tasteMagnitude, candidateMagnitude);
        double novelty = familiarArtist ? 0 : 1;
        double bonus = novelty * configuration.NoveltyWeight;
        if (!double.IsFinite(tagScore + bonus)) throw new ArgumentException("Nonfinite score arithmetic.");
        var missing = new List<string>();
        if (tasteMagnitude == 0) missing.Add("empty_taste_vector");
        if (candidateMagnitude == 0) missing.Add("empty_candidate_vector");
        return new ScoreBreakdown(tagScore + bonus, raw, tagScore, 0, 0, novelty, bonus, 0, 0,
            contributions.Select(c => new MatchedTagContribution(c.TagName, c.CandidateWeight, c.TasteWeight,
                c.CandidateWeight * c.TasteWeight)).ToArray())
        {
            Snapshot = new ScoreSnapshot
            {
                FormulaVersion = configuration.FormulaVersion,
                RankingPolicyVersion = candidate.Evidence.SelectionPolicy ?? "score-ordinal-one-artist-v1",
                MaterializationVersion = candidate.Evidence.MaterializationVersion,
                ConfigurationVersion = configuration.Version,
                ConfigurationSha256 = inputs?.Hash(configuration) ?? configuration.Sha256,
                ConfigurationPolicy = configuration.Policy ?? new Dictionary<string, string>(),
                TagNormalizationVersion = candidate.Evidence.TagNormalizationVersion,
                Weights = new(configuration.TagWeight, configuration.NoveltyWeight),
                TrackKey = candidate.Track.TrackKey,
                ArtistName = candidate.Track.ArtistName,
                Title = candidate.Track.Title,
                Week = week,
                DiscoveryBatchId = batchId,
                PayloadVersion = batchId.HasValue ? "discovery-score-v2" : "phase6-score-v1",
                TasteWeights = tasteWeights.Count <= 80 ? tasteWeights : new SortedDictionary<string, double>(tasteWeights.Where(p => candidateWeights.ContainsKey(p.Key)).ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal),
                UnmatchedTasteSquared = tasteWeights.Count <= 80 ? (unmatchedTasteSquared == 0 ? null : unmatchedTasteSquared) : tasteWeights.Where(p => !candidateWeights.ContainsKey(p.Key)).Sum(p => p.Value * p.Value) + unmatchedTasteSquared,
                CandidateWeights = candidateWeights,
                TasteMagnitude = tasteMagnitude,
                CandidateMagnitude = candidateMagnitude,
                Familiarity = familiarArtist,
                Contributions = contributions,
                Components = [new("tag_similarity", tagScore), new("novelty", bonus)],
                MissingReasons = missing.Concat(candidate.Evidence.Seeds.Where(s => s.MissingReason != null)
                    .Select(s => $"seed:{s.ArtistName}:{s.MissingReason}")).Distinct(StringComparer.Ordinal).ToArray(),
                Evidence = candidate.Evidence
            }
        };
    }

    public IReadOnlyList<BaselineAPick> Rank(IEnumerable<BaselineACandidate> candidates, WeightedTagVector taste,
        IReadOnlySet<string> familiarArtists, IReadOnlySet<string> excludedAliases, IsoWeek week,
        BaselineAConfiguration configuration, int count = 5)
    {
        if (count is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(count));
        return Score(candidates.Where(c => !TrackIdentity.Aliases(c.Track).Any(excludedAliases.Contains)), taste,
                familiarArtists, week, configuration)
            .OrderByDescending(c => c.Breakdown.FinalScore).ThenBy(c => c.Track.TrackKey, StringComparer.Ordinal)
            .DistinctBy(c => c.Track.NormalizedArtistName, StringComparer.Ordinal).Take(count).ToArray();
    }

    public IReadOnlyList<BaselineAPick> RankForBatch(IEnumerable<BaselineACandidate> candidates, WeightedTagVector taste,
        IReadOnlySet<string> familiarArtists, IReadOnlySet<string> excludedAliases, Guid batchId,
        BaselineAConfiguration configuration, int count = 5)
    {
        if (count is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(count));
        return ScoreForBatch(candidates.Where(c => !TrackIdentity.Aliases(c.Track).Any(excludedAliases.Contains)), taste,
                familiarArtists, batchId, configuration)
            .OrderByDescending(c => c.Breakdown.FinalScore).ThenBy(c => c.Track.TrackKey, StringComparer.Ordinal)
            .DistinctBy(c => c.Track.NormalizedArtistName, StringComparer.Ordinal).Take(count).ToArray();
    }

    public ScoreBreakdown Replay(ScoreSnapshot snapshot)
    {
        if (snapshot.PayloadVersion is not ("phase6-score-v1" or "discovery-score-v2") || snapshot.FormulaVersion is not ("baseline-a-v1" or "baseline-a-v2") ||
            snapshot.MaterializationVersion is not ("positive-signals-v1" or "canonical-entity-max-v1" or "canonical-entity-max-v2") ||
            snapshot.TagNormalizationVersion is not ("max-observed-descriptive-count-v1" or "max-observed-artist-tag-votes-v1") ||
            snapshot.RankingPolicyVersion is not ("score-ordinal-one-artist-v1" or "optional-taste-selection-v1"))
            throw new NotSupportedException("Unsupported stored scoring version.");
        var configuration = new BaselineAConfiguration(snapshot.ConfigurationVersion, snapshot.Weights.Tag,
            snapshot.Weights.Novelty, snapshot.ConfigurationPolicy, snapshot.FormulaVersion);
        if (configuration.Sha256 != snapshot.ConfigurationSha256) throw new ArgumentException("Stored configuration hash mismatch.");
        string? mbid = snapshot.TrackKey.StartsWith("mbid:", StringComparison.Ordinal) ? snapshot.TrackKey[5..] : null;
        var track = Track.Create(snapshot.Title, snapshot.ArtistName, mbid: mbid);
        if (track.TrackKey != snapshot.TrackKey) throw new ArgumentException("Stored track identity mismatch.");
        ValidateWeights(snapshot.TasteWeights); ValidateWeights(snapshot.CandidateWeights);
        if (snapshot.PayloadVersion == "discovery-score-v2" && (snapshot.Week is not null || !snapshot.DiscoveryBatchId.HasValue || snapshot.DiscoveryBatchId == Guid.Empty))
            throw new ArgumentException("Invalid discovery snapshot identity.");
        return ScoreCore(new(track, WeightedTagVector.FromDictionary(snapshot.CandidateWeights), snapshot.Evidence),
            WeightedTagVector.FromDictionary(snapshot.TasteWeights), snapshot.Familiarity,
            snapshot.PayloadVersion == "phase6-score-v1" ? IsoWeek.Parse(snapshot.Week!).Value : null,
            snapshot.PayloadVersion == "discovery-score-v2" ? snapshot.DiscoveryBatchId : null, configuration, snapshot.UnmatchedTasteSquared ?? 0);
    }

    private static SortedDictionary<string, double> Ordered(WeightedTagVector vector)
    {
        ValidateWeights(vector.Weights);
        return new(vector.Weights.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal);
    }

    private static void ValidateWeights(IReadOnlyDictionary<string, double> weights)
    {
        if (weights.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Key != p.Key.Trim().ToLowerInvariant() || !double.IsFinite(p.Value) || p.Value <= 0))
            throw new ArgumentException("Tag vectors require normalized names and finite positive weights.");
    }

    private static double Magnitude(IReadOnlyDictionary<string, double> vector)
    {
        double magnitude = Math.Sqrt(vector.Values.Sum(v => v * v));
        if (!double.IsFinite(magnitude) || vector.Count > 0 && magnitude == 0)
            throw new ArgumentException("Nonfinite or underflowed vector magnitude.");
        return magnitude;
    }

    private static double Cosine(IReadOnlyDictionary<string, double> taste, IReadOnlyDictionary<string, double> candidate,
        double tasteMagnitude, double candidateMagnitude) => tasteMagnitude == 0 || candidateMagnitude == 0 ? 0 :
        candidate.Where(p => taste.ContainsKey(p.Key)).Sum(p => p.Value / candidateMagnitude * (taste[p.Key] / tasteMagnitude));
}

public static class TrackIdentity
{
    public static IReadOnlyList<string> Aliases(Track track) =>
        [track.TrackKey, $"{track.NormalizedArtistName}:{track.NormalizedTitle}"];
}
