using FluentValidation;

namespace LinerNotes.Application.Features.Digests.Commands.RecordFeedback;

/// <summary>
/// FluentValidation validator ensuring valid recommendation feedback arguments.
/// </summary>
public sealed class RecordRecommendationFeedbackCommandValidator : AbstractValidator<RecordRecommendationFeedbackCommand>
{
    public RecordRecommendationFeedbackCommandValidator()
    {
        RuleFor(x => x.RecommendationId)
            .NotEmpty().WithMessage("RecommendationId is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.Feedback)
            .IsInEnum().WithMessage("A valid UserFeedback option must be provided.");

        RuleFor(x => x.Comment)
            .MaximumLength(1000).WithMessage("Feedback comment cannot exceed 1000 characters.");

        RuleFor(x => x.Rating)
            .InclusiveBetween(1, 10).When(x => x.Rating.HasValue)
            .WithMessage("Rating must be between 1 and 10.");
    }
}
