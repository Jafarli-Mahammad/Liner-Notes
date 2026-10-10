using System.Text.Json;
using System.Text.Json.Serialization;

namespace LinerNotes.Domain.Scoring;

// These are stored data shapes only. Serialization is performed at the persistence boundary.
public record ExtensibleScoreRecord
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? UnknownFields { get; init; }
}

public sealed record ScoreWeights(double Tag, double Novelty) : ExtensibleScoreRecord;
public sealed record NormalizedTagContribution(string TagName, double CandidateWeight,
    double TasteWeight, double NormalizedWeightedContribution) : ExtensibleScoreRecord;
public sealed record ScoreComponent(string Name, double Value) : ExtensibleScoreRecord;
public sealed record StoredResponse(string Provider, string Method, string RequestIdentity,
    string Origin, string? ResponseSha256, DateTimeOffset? RetrievedAtUtc) : ExtensibleScoreRecord;
public sealed record StoredObservation(string? Value, string? RawValue, string? MissingReason) : ExtensibleScoreRecord;
public sealed record StoredTag(string Name, StoredObservation Count, string? Url,
    StoredResponse Response, string? ExclusionReason, int UpstreamPosition) : ExtensibleScoreRecord;
public sealed record StoredGap(string Seed, string Method, string Reason, StoredResponse? Response) : ExtensibleScoreRecord;
public sealed record StoredDiscoveryPath(string Seed, StoredObservation Match,
    StoredResponse? SimilarityResponse, StoredResponse TrackResponse, int UpstreamPosition,
    string? TrackUrl, string? ArtistUrl, StoredObservation TrackListeners, string? RawRank,
    string? DiscoveredArtist, int? SimilarityPosition) : ExtensibleScoreRecord;
public sealed record StoredTasteSignal(Guid Id, string TargetType, string TargetValue, double Weight,
    string Source, string Context) : ExtensibleScoreRecord;
public sealed record StoredSeed(string ArtistName, IReadOnlyDictionary<string, double> Weights,
    IReadOnlyList<StoredTag> Tags, StoredResponse Response, string? MissingReason,
    IReadOnlyList<StoredGap> Gaps) : ExtensibleScoreRecord;
public sealed record ScoreEvidence(IReadOnlyList<StoredTasteSignal> Signals,
    IReadOnlyList<StoredSeed> Seeds, IReadOnlyList<StoredTag> CandidateTags,
    StoredResponse? CandidateTagResponse, IReadOnlyList<StoredDiscoveryPath> DiscoveryPaths,
    IReadOnlyList<StoredGap> Gaps, string TagScope, string PopularityMissingReason) : ExtensibleScoreRecord
{
    public static ScoreEvidence Empty { get; } = new([], [], [], null, [], [], "artist", "not_supplied");
}

public sealed record ScoreSnapshot : ExtensibleScoreRecord
{
    public string PayloadVersion { get; init; } = "phase6-score-v1";
    public string FormulaVersion { get; init; } = "baseline-a-v1";
    public required string ConfigurationVersion { get; init; }
    public required string ConfigurationSha256 { get; init; }
    public IReadOnlyDictionary<string, string> ConfigurationPolicy { get; init; } = new Dictionary<string, string>();
    public required ScoreWeights Weights { get; init; }
    public string MaterializationVersion { get; init; } = "positive-signals-v1";
    public string TagNormalizationVersion { get; init; } = "max-observed-descriptive-count-v1";
    public string RankingPolicyVersion { get; init; } = "score-ordinal-one-artist-v1";
    public required string TrackKey { get; init; }
    public required string ArtistName { get; init; }
    public required string Title { get; init; }
    public required string Week { get; init; }
    public required IReadOnlyDictionary<string, double> TasteWeights { get; init; }
    public required IReadOnlyDictionary<string, double> CandidateWeights { get; init; }
    public double TasteMagnitude { get; init; }
    public double CandidateMagnitude { get; init; }
    public bool Familiarity { get; init; }
    public IReadOnlyList<NormalizedTagContribution> Contributions { get; init; } = [];
    public IReadOnlyList<ScoreComponent> Components { get; init; } = [];
    public IReadOnlyList<string> MissingReasons { get; init; } = [];
    public ScoreEvidence Evidence { get; init; } = ScoreEvidence.Empty;
}
