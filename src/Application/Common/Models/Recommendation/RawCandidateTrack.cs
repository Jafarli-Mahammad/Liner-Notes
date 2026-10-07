using System.Collections.Immutable;
using LinerNotes.Domain.Catalog;

namespace LinerNotes.Application.Common.Models.Recommendation;

/// <summary>Unmapped discovery metadata. Every path and original observation is retained.</summary>
public sealed class RawCandidateTrack
{
    public string Title { get; }
    public string ArtistName { get; }
    public string? Mbid { get; }
    public ImmutableArray<DiscoveryPath> Paths { get; }
    public EvidenceOrigin Origin => Paths[0].TrackResponse.Origin;
    // No match aggregation policy is adopted in Phase 3.
    public double? UpstreamScore => Paths.Length == 1 ? Paths[0].Match.Value : null;
    public string SourceId => $"{Paths[0].TrackResponse.Provider.ToLowerInvariant().Replace(".", "", StringComparison.Ordinal)}:{(Paths[0].SimilarityResponse ?? Paths[0].TrackResponse).Method.ToLowerInvariant()}";
    public string CandidateKey { get; }

    public RawCandidateTrack(string title, string artistName, string? mbid, IEnumerable<DiscoveryPath> paths)
    {
        var track = Track.Create(title, artistName, mbid: mbid);
        Title = track.Title; ArtistName = track.ArtistName; Mbid = track.Mbid; CandidateKey = track.TrackKey;
        Paths = paths.OrderBy(p => p.Seed.Trim().ToLowerInvariant(), StringComparer.Ordinal)
            .ThenBy(p => p.TrackResponse.Method, StringComparer.Ordinal)
            .ThenBy(p => p.SimilarityResponse?.ResponseSha256, StringComparer.Ordinal)
            .ThenBy(p => p.TrackResponse.ResponseSha256, StringComparer.Ordinal)
            .ThenBy(p => p.UpstreamPosition).ThenBy(p => p.SimilarityPosition).ToImmutableArray();
        if (Paths.IsEmpty) throw new ArgumentException("Candidate must have response provenance.", nameof(paths));
        static bool Received(ResponseReference reference) => !string.IsNullOrWhiteSpace(reference.Provider) &&
            !string.IsNullOrWhiteSpace(reference.Method) && !string.IsNullOrWhiteSpace(reference.RequestIdentity) &&
            Enum.IsDefined(reference.Origin) && reference.ResponseSha256 is { Length: 64 } hash &&
            hash.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') &&
            reference.RetrievedAtUtc is { Offset: var offset } && offset == TimeSpan.Zero;
        if (Paths.Any(p => p.UpstreamPosition <= 0 || !Received(p.TrackResponse) ||
            (p.SimilarityResponse is { } reference && !Received(reference))))
            throw new ArgumentException("Emitted candidates require received response hashes, timestamps and positions.", nameof(paths));
        if (Paths.Any(p => p.TrackResponse.Origin != Origin ||
            (p.SimilarityResponse is { } reference && reference.Origin != Origin)))
            throw new InvalidOperationException("Live, recorded and synthetic observations cannot be blended.");
    }
}
