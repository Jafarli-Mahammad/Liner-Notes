using LinerNotes.Domain.Enums;

namespace LinerNotes.Application.DTOs.Digests;

/// <summary>
/// Immutable DTO representing a subscriber's complete weekly discovery digest.
/// </summary>
public record WeeklyDigestDto(
    Guid Id,
    Guid UserId,
    string Week,
    DateTime WeekStartDate,
    DateTime WeekEndDate,
    DigestStatus Status,
    DateTime? SentAt,
    IReadOnlyList<WeeklyRecommendationDto> Recommendations,
    DateTime CreatedAt = default,
    DateTime? LastModifiedAt = null,
    string? ErrorMessage = null,
    Guid? CreatedBy = null,
    Guid? LastModifiedBy = null,
    Guid? DeletedBy = null,
    DateTime? DeletedAt = null,
    bool IsDeleted = false);
