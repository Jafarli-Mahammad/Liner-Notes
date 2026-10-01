using LinerNotes.Application.DTOs.Taste;
using MediatR;

namespace LinerNotes.Application.Features.Taste.Commands.SeedTasteProfile;

/// <summary>
/// Command to initialize a user's taste profile using 3–5 seed tags and/or artists (hybrid onboarding pillar).
/// </summary>
public record SeedTasteProfileCommand(
    Guid UserId,
    IReadOnlyList<SeedTagDto> Tags,
    IReadOnlyList<SeedArtistDto> Artists) : IRequest<int>;
