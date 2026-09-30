using LinerNotes.Domain.Common;

namespace LinerNotes.Domain.Catalog;

/// <summary>
/// Individual musical track entity containing metadata for recommendation mapping.
/// Avoids audio-level internals (waveforms, embeddings, bitrates).
/// </summary>
public sealed class Track : BaseEntity
{
    public string Title { get; private set; }
    public string NormalizedTitle { get; private set; }
    public string ArtistName { get; private set; }
    public string NormalizedArtistName { get; private set; }
    public string? AlbumTitle { get; private set; }
    public string? Mbid { get; private set; }
    public int? DurationSeconds { get; private set; }
    public string? ExternalSpotifyUrl { get; private set; }
    public string? ExternalYoutubeUrl { get; private set; }

    /// <summary>
    /// Deterministic track key used for identity comparisons and tie-breaking in ranking.
    /// Prefers MBID when present; falls back to "normalized_artist:normalized_title".
    /// </summary>
    public string TrackKey => !string.IsNullOrEmpty(Mbid)
        ? $"mbid:{Mbid.ToLowerInvariant()}"
        : $"{NormalizedArtistName}:{NormalizedTitle}";

    // Parameterless constructor for EF Core
    private Track()
    {
        Title = string.Empty;
        NormalizedTitle = string.Empty;
        ArtistName = string.Empty;
        NormalizedArtistName = string.Empty;
    }

    public Track(
        string title,
        string artistName,
        string? albumTitle = null,
        string? mbid = null,
        int? durationSeconds = null,
        string? externalSpotifyUrl = null,
        string? externalYoutubeUrl = null,
        Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Track title cannot be empty.", nameof(title));
        if (string.IsNullOrWhiteSpace(artistName))
            throw new ArgumentException("Artist name cannot be empty.", nameof(artistName));

        if (id.HasValue) Id = id.Value;
        Title = title.Trim();
        NormalizedTitle = Title.ToLowerInvariant();
        ArtistName = artistName.Trim();
        NormalizedArtistName = ArtistName.ToLowerInvariant();
        AlbumTitle = string.IsNullOrWhiteSpace(albumTitle) ? null : albumTitle.Trim();
        Mbid = string.IsNullOrWhiteSpace(mbid) ? null : mbid.Trim();
        DurationSeconds = durationSeconds;
        ExternalSpotifyUrl = string.IsNullOrWhiteSpace(externalSpotifyUrl) ? null : externalSpotifyUrl.Trim();
        ExternalYoutubeUrl = string.IsNullOrWhiteSpace(externalYoutubeUrl) ? null : externalYoutubeUrl.Trim();
    }

    public static Track Create(
        string title,
        string artistName,
        string? albumTitle = null,
        string? mbid = null) =>
        new(title, artistName, albumTitle, mbid);

    public override string ToString() => $"{ArtistName} - {Title}";
}
