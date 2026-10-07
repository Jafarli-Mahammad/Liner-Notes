using System.Collections;
using System.Collections.Immutable;
using LinerNotes.Domain.Catalog;

namespace LinerNotes.Application.Common.Models.Recommendation;

public enum EvidenceOrigin { Live, Recorded, Synthetic }

public sealed record ObservedValue<T>(T? Value, string? RawValue, string? MissingReason) where T : struct
{
    public bool IsValid => Value.HasValue && MissingReason is null;
    public static ObservedValue<T> Missing(string reason = "missing", string? raw = null) => new(null, raw, reason);
}

// Request identity is a seed/artist/tag, never a URL containing credentials.
public sealed record ResponseReference(string Provider, string Method, string RequestIdentity,
    EvidenceOrigin Origin, string? ResponseSha256, DateTimeOffset? RetrievedAtUtc);

public sealed record CoverageGap(string Seed, string Method, string Reason, ResponseReference? Response = null);

public sealed class IngestionResult<T> : IReadOnlyList<T>
{
    public ImmutableArray<T> Items { get; }
    public ImmutableArray<CoverageGap> Gaps { get; }
    public int Count => Items.Length;
    public T this[int index] => Items[index];
    public IngestionResult(IEnumerable<T> items, IEnumerable<CoverageGap>? gaps = null)
    {
        Items = items.ToImmutableArray();
        Gaps = gaps?.ToImmutableArray() ?? [];
    }
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed record DiscoveryPath(string Seed, ObservedValue<double> Match,
    ResponseReference? SimilarityResponse, ResponseReference TrackResponse, int UpstreamPosition,
    string? TrackUrl, string? ArtistUrl, ObservedValue<long> TrackListeners,
    string? RawRank = null, string? DiscoveredArtist = null, int? SimilarityPosition = null);

public sealed record TagObservation(string Name, ObservedValue<long> Count, string? Url,
    ResponseReference Response, string? ExclusionReason, int UpstreamPosition);

/// <summary>Transient ingestion snapshot, deliberately separate from the legacy scorer input.</summary>
public sealed class HydratedCandidateTrack
{
    public Track Track { get; }
    public WeightedTagVector TagVector { get; }
    public RawCandidateTrack RawCandidate { get; }
    public ImmutableArray<TagObservation> Tags { get; }
    public ResponseReference TagResponse { get; }
    public string TagScope => "artist";
    public string TagNormalizationVersion => "max-observed-descriptive-count-v1";
    public string? TagMissingReason { get; }
    public ImmutableArray<CoverageGap> Gaps { get; }
    public double? GlobalPopularity => null;
    public string PopularityMissingReason => "artist_listener_measure_not_supplied";
    public EvidenceOrigin Origin => RawCandidate.Origin;

    public HydratedCandidateTrack(Track track, WeightedTagVector vector, RawCandidateTrack raw,
        IEnumerable<TagObservation> tags, ResponseReference response, string? tagMissingReason,
        IEnumerable<CoverageGap>? gaps = null)
    {
        Track = track; TagVector = vector; RawCandidate = raw; Tags = tags.ToImmutableArray();
        TagResponse = response; TagMissingReason = tagMissingReason;
        Gaps = gaps?.ToImmutableArray() ?? [];
        if (raw.Origin != response.Origin) throw new InvalidOperationException("Cannot blend response origins.");
    }
}
