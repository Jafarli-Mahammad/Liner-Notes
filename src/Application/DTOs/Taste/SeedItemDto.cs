namespace LinerNotes.Application.DTOs.Taste;

/// <summary>
/// Seed tag DTO used during cold-start taste profile initialization.
/// </summary>
public record SeedTagDto(
    string Tag,
    double Weight = 1.0,
    string? Context = null);

/// <summary>
/// Seed artist DTO used during cold-start taste profile initialization.
/// </summary>
public record SeedArtistDto(
    string ArtistName,
    double Weight = 1.0,
    string? Context = null);
