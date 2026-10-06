namespace LinerNotes.Application.Common.Models.Recommendation;

/// <summary>
/// An unhydrated music candidate discovered from an upstream recommendation source (e.g. Last.fm, ListenBrainz).
/// Contains upstream affinity scores before local tag hydration, popularity penalties, and feedback re-ranking.
/// </summary>
/// <param name="Title">The song or track title.</param>
/// <param name="ArtistName">The performing artist or band name.</param>
/// <param name="Mbid">MusicBrainz Identifier if available from upstream metadata.</param>
/// <param name="UpstreamScore">The raw affinity or similarity score provided by upstream (0.0 to 1.0).</param>
/// <param name="SourceId">Identifier of the upstream source (e.g. "lastfm:artist.getSimilar").</param>
public sealed record RawCandidateTrack(
    string Title,
    string ArtistName,
    string? Mbid,
    double UpstreamScore,
    string SourceId)
{
    /// <summary>
    /// Deterministic candidate lookup key matching the domain TrackKey format.
    /// </summary>
    public string CandidateKey => !string.IsNullOrEmpty(Mbid)
        ? $"mbid:{Mbid.ToLowerInvariant()}"
        : $"{ArtistName.Trim().ToLowerInvariant()}:{Title.Trim().ToLowerInvariant()}";
}
