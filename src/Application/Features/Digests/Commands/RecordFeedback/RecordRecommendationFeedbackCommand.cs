using LinerNotes.Domain.Enums;
using MediatR;

namespace LinerNotes.Application.Features.Digests.Commands.RecordFeedback;

/// <summary>
/// Command to record 1-click feedback (Thumbs Up, Thumbs Down, Already Know This) on a recommendation.
/// </summary>
public record RecordRecommendationFeedbackCommand(
    Guid RecommendationId,
    Guid UserId,
    UserFeedback Feedback,
    string? Comment = null,
    int? Rating = null) : IRequest<bool>;
