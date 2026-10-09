using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;

namespace LinerNotes.Domain.Scoring;

public sealed record BaselineAConfiguration(string Version = "phase6-config-v1", double TagWeight = 1,
    double NoveltyWeight = 0.2, IReadOnlyDictionary<string, string>? Policy = null)
{
    public string Sha256
    {
        get
        {
            Validate();
            var values = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["version"] = Version, ["formula"] = "baseline-a-v1",
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
        if (string.IsNullOrWhiteSpace(Version) || !double.IsFinite(TagWeight) || TagWeight < 0 ||
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
    {
        configuration.Validate();
        var tasteWeights = Ordered(taste);
        var candidateWeights = Ordered(candidate.Tags);
        double tasteMagnitude = Magnitude(tasteWeights), candidateMagnitude = Magnitude(candidateWeights);
        var contributions = candidateWeights.Where(p => tasteWeights.ContainsKey(p.Key)).Select(p =>
            new NormalizedTagContribution(p.Key, p.Value, tasteWeights[p.Key],
                p.Value / candidateMagnitude * (tasteWeights[p.Key] / tasteMagnitude) * configuration.TagWeight)).ToArray();
        double tagScore = contributions.Sum(p => p.NormalizedWeightedContribution);
        double raw = configuration.TagWeight == 0 ? Cosine(tasteWeights, candidateWeights, tasteMagnitude, candidateMagnitude) : tagScore / configuration.TagWeight;
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
                ConfigurationVersion = configuration.Version, ConfigurationSha256 = configuration.Sha256,
                ConfigurationPolicy = configuration.Policy ?? new Dictionary<string, string>(),
                Weights = new(configuration.TagWeight, configuration.NoveltyWeight), TrackKey = candidate.Track.TrackKey,
                ArtistName = candidate.Track.ArtistName, Title = candidate.Track.Title, Week = week.Value,
                TasteWeights = tasteWeights, CandidateWeights = candidateWeights,
                TasteMagnitude = tasteMagnitude, CandidateMagnitude = candidateMagnitude, Familiarity = familiarArtist,
                Contributions = contributions, Components = [new("tag_similarity", tagScore), new("novelty", bonus)],
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
        return candidates.Where(c => !TrackIdentity.Aliases(c.Track).Any(excludedAliases.Contains))
            .Select(c => new BaselineAPick(c.Track, Score(c, taste,
                familiarArtists.Contains(c.Track.NormalizedArtistName), week, configuration)))
            .OrderByDescending(c => c.Breakdown.FinalScore).ThenBy(c => c.Track.TrackKey, StringComparer.Ordinal)
            .DistinctBy(c => c.Track.NormalizedArtistName, StringComparer.Ordinal).Take(count).ToArray();
    }

    public ScoreBreakdown Replay(ScoreSnapshot snapshot)
    {
        if (snapshot.PayloadVersion != "phase6-score-v1" || snapshot.FormulaVersion != "baseline-a-v1" ||
            snapshot.MaterializationVersion != "positive-signals-v1" ||
            snapshot.TagNormalizationVersion != "max-observed-descriptive-count-v1" ||
            snapshot.RankingPolicyVersion != "score-ordinal-one-artist-v1")
            throw new NotSupportedException("Unsupported stored scoring version.");
        var configuration = new BaselineAConfiguration(snapshot.ConfigurationVersion, snapshot.Weights.Tag,
            snapshot.Weights.Novelty, snapshot.ConfigurationPolicy);
        if (configuration.Sha256 != snapshot.ConfigurationSha256) throw new ArgumentException("Stored configuration hash mismatch.");
        string? mbid = snapshot.TrackKey.StartsWith("mbid:", StringComparison.Ordinal) ? snapshot.TrackKey[5..] : null;
        var track = Track.Create(snapshot.Title, snapshot.ArtistName, mbid: mbid);
        if (track.TrackKey != snapshot.TrackKey) throw new ArgumentException("Stored track identity mismatch.");
        ValidateWeights(snapshot.TasteWeights); ValidateWeights(snapshot.CandidateWeights);
        return Score(new(track, WeightedTagVector.FromDictionary(snapshot.CandidateWeights), snapshot.Evidence),
            WeightedTagVector.FromDictionary(snapshot.TasteWeights), snapshot.Familiarity, IsoWeek.Parse(snapshot.Week), configuration);
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
