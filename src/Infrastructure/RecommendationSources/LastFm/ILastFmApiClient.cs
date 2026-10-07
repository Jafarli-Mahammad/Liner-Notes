using LinerNotes.Infrastructure.RecommendationSources.LastFm.Models;

namespace LinerNotes.Infrastructure.RecommendationSources.LastFm;

/// <summary>
/// Low-level typed HTTP client for Last.fm Web Services 2.0.
/// Returns explicit provenance and gaps; never substitutes synthetic data after a real failure.
/// </summary>
public interface ILastFmApiClient
{
    /// <summary>
    /// Calls 'artist.getSimilar' to retrieve artists similar to the specified artist name.
    /// </summary>
    Task<LastFmResponse<LastFmArtistSummary>> GetSimilarArtistsAsync(
        string artistName,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls 'artist.getTopTags' to retrieve top user tags for the specified artist.
    /// </summary>
    Task<LastFmResponse<LastFmTagItem>> GetArtistTopTagsAsync(
        string artistName,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls 'artist.getTopTracks' to retrieve top tracks for the specified artist.
    /// </summary>
    Task<LastFmResponse<LastFmTrackItem>> GetArtistTopTracksAsync(
        string artistName,
        int limit = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls 'tag.getTopTracks' to retrieve top tracks tagged with the specified musical tag.
    /// </summary>
    Task<LastFmResponse<LastFmTrackItem>> GetTagTopTracksAsync(
        string tag,
        int limit = 10,
        CancellationToken cancellationToken = default);
}
