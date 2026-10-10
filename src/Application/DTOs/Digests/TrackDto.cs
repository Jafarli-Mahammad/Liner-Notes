namespace LinerNotes.Application.DTOs.Digests;

/// <summary>
/// Immutable DTO representing musical track catalog metadata and 1-click external listening links.
/// </summary>
public record TrackDto(
    Guid Id,
    string Title,
    string ArtistName,
    string? AlbumTitle,
    string? Mbid,
    int? DurationSeconds,
    string? ExternalSpotifyUrl,
    string? ExternalYoutubeUrl,
    string NormalizedTitle = "",
    string NormalizedArtistName = "");
