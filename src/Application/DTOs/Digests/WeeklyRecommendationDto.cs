using LinerNotes.Domain.Enums;

namespace LinerNotes.Application.DTOs.Digests;

/// <summary>
/// Immutable DTO representing an individual weekly pick, its rank, score breakdown, and user feedback.
/// </summary>
public record WeeklyRecommendationDto(
    Guid Id,
    int Rank,
    TrackDto Track,
    ScoreBreakdownDto ScoreBreakdown,
    string WhyThisPick,
    UserFeedback Feedback,
    string? FeedbackComment,
    DateTime? FeedbackGivenAt);
