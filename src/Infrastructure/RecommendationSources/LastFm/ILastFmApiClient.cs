using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

/// <summary>
/// Low-level typed HTTP client for Last.fm Web Services 2.0.
/// Encapsulates request rate limiting, response caching, error backoff, and hybrid fixture fallback.
/// </summary>
public interface ILastFmApiClient
{
    /// <summary>
    /// Calls 'artist.getSimilar' to retrieve artists similar to the specified artist name.
    /// </summary>
    Task<IReadOnlyList<LastFmArtistSummary>> GetSimilarArtistsAsync(
        string artistName,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls 'artist.getTopTags' to retrieve top user tags for the specified artist.
    /// </summary>
    Task<IReadOnlyList<LastFmTagItem>> GetArtistTopTagsAsync(
        string artistName,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls 'artist.getTopTracks' to retrieve top tracks for the specified artist.
    /// </summary>
    Task<IReadOnlyList<LastFmTrackItem>> GetArtistTopTracksAsync(
        string artistName,
        int limit = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls 'tag.getTopTracks' to retrieve top tracks tagged with the specified musical tag.
    /// </summary>
    Task<IReadOnlyList<LastFmTrackItem>> GetTagTopTracksAsync(
        string tag,
        int limit = 10,
        CancellationToken cancellationToken = default);
}
