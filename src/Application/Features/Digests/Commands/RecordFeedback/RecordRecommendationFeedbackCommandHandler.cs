using LinerNotes.Application.Common.Exceptions;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Domain.Digest;
using MediatR;

namespace LinerNotes.Application.Features.Digests.Commands.RecordFeedback;

/// <summary>
/// Handler recording explicit subscriber feedback on a recommended track.
/// </summary>
public sealed class RecordRecommendationFeedbackCommandHandler : IRequestHandler<RecordRecommendationFeedbackCommand, bool>
{
    private readonly IWeeklyDigestRepository _weeklyDigestRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordRecommendationFeedbackCommandHandler(
        IWeeklyDigestRepository weeklyDigestRepository,
        IUnitOfWork unitOfWork)
    {
        _weeklyDigestRepository = weeklyDigestRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(
        RecordRecommendationFeedbackCommand request,
        CancellationToken cancellationToken)
    {
        var recommendation = await _weeklyDigestRepository.GetRecommendationByIdAsync(
            request.RecommendationId,
            request.UserId,
            cancellationToken);

        if (recommendation is null)
        {
            throw new NotFoundException(nameof(WeeklyRecommendation), request.RecommendationId);
        }

        recommendation.RecordFeedback(request.Feedback, request.Comment);
        _weeklyDigestRepository.UpdateRecommendation(recommendation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
