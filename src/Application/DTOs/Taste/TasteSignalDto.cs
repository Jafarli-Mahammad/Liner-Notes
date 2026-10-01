using LinerNotes.Domain.Enums;

namespace LinerNotes.Application.DTOs.Taste;

/// <summary>
/// Immutable DTO representing a discrete taste signal and its provenance.
/// </summary>
public record TasteSignalDto(
    Guid Id,
    Guid UserId,
    TasteTargetType TargetType,
    string TargetValue,
    string NormalizedTargetValue,
    double Weight,
    TasteSignalSource Source,
    string Context,
    DateTime CreatedAt);
