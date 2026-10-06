using LinerNotes.Application.Common.Models.Recommendation;

namespace LinerNotes.Application.Common.Interfaces.Recommendation;

/// <summary>
/// Abstraction for upstream music candidate discovery sources (e.g. Last.fm, ListenBrainz).
/// Keeps upstream service contracts, rate limits, and terms changes isolated from Domain and Application orchestration.
/// </summary>
public interface IRecommendationSource
{
    /// <summary>
    /// Gets the unique upstream provider name (e.g. "Last.fm", "ListenBrainz").
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Discovers candidate tracks based on a list of seed artist names.
    /// </summary>
    /// <param name="artistNames">Collection of seed artist names.</param>
    /// <param name="limitPerArtist">Maximum number of candidate tracks to retrieve per seed artist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of raw unhydrated candidate tracks.</returns>
    Task<IReadOnlyList<RawCandidateTrack>> GetCandidatesByArtistsAsync(
        IReadOnlyList<string> artistNames,
        int limitPerArtist = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers candidate tracks based on a list of seed musical tags or genres.
    /// </summary>
    /// <param name="tags">Collection of seed musical tags.</param>
    /// <param name="limitPerTag">Maximum number of candidate tracks to retrieve per seed tag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of raw unhydrated candidate tracks.</returns>
    Task<IReadOnlyList<RawCandidateTrack>> GetCandidatesByTagsAsync(
        IReadOnlyList<string> tags,
        int limitPerTag = 10,
        CancellationToken cancellationToken = default);
}
