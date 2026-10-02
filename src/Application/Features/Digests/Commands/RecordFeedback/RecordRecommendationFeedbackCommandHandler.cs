using LinerNotes.Application.Common.Exceptions;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;
using MediatR;

namespace LinerNotes.Application.Features.Digests.Commands.RecordFeedback;

/// <summary>
/// Handler recording explicit subscriber feedback on a recommended track.
/// Generates provenance-tagged TasteSignals to reinforce or adjust future recommendation scoring.
/// </summary>
public sealed class RecordRecommendationFeedbackCommandHandler : IRequestHandler<RecordRecommendationFeedbackCommand, bool>
{
    private readonly IWeeklyDigestRepository _weeklyDigestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITasteSignalRepository? _tasteSignalRepository;

    public RecordRecommendationFeedbackCommandHandler(
        IWeeklyDigestRepository weeklyDigestRepository,
        IUnitOfWork unitOfWork,
        ITasteSignalRepository? tasteSignalRepository = null)
    {
        _weeklyDigestRepository = weeklyDigestRepository;
        _unitOfWork = unitOfWork;
        _tasteSignalRepository = tasteSignalRepository;
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

        recommendation.RecordFeedback(request.Feedback, request.Comment, request.Rating);
        _weeklyDigestRepository.UpdateRecommendation(recommendation);

        if (_tasteSignalRepository is not null)
        {
            var signals = new List<TasteSignal>();
            var artistName = recommendation.Track.ArtistName;
            var trackKey = recommendation.Track.TrackKey;

            if (request.Feedback == UserFeedback.Liked || (request.Rating.HasValue && request.Rating.Value >= 7))
            {
                double weight = request.Rating.HasValue ? Math.Clamp((request.Rating.Value - 5) / 5.0, 0.4, 1.0) : 1.0;
                signals.Add(new TasteSignal(
                    request.UserId,
                    TasteTargetType.Artist,
                    artistName,
                    weight,
                    TasteSignalSource.RecommendationLike,
                    $"Liked recommendation #{recommendation.Rank}: {recommendation.Track.Title}"));

                if (recommendation.ScoreBreakdown?.MatchedTags is not null)
                {
                    foreach (var matchedTag in recommendation.ScoreBreakdown.MatchedTags.Take(3))
                    {
                        signals.Add(new TasteSignal(
                            request.UserId,
                            TasteTargetType.Tag,
                            matchedTag.TagName,
                            weight * 0.5,
                            TasteSignalSource.RecommendationLike,
                            $"Nudged tag from liked recommendation #{recommendation.Rank}"));
                    }
                }
            }
            else if (request.Feedback == UserFeedback.Disliked || (request.Rating.HasValue && request.Rating.Value <= 3))
            {
                double penalty = request.Rating.HasValue ? Math.Clamp((5 - request.Rating.Value) / 5.0, 0.4, 1.0) : 1.0;
                signals.Add(new TasteSignal(
                    request.UserId,
                    TasteTargetType.Track,
                    trackKey,
                    -penalty,
                    TasteSignalSource.RecommendationDislike,
                    $"Disliked recommendation #{recommendation.Rank}: {recommendation.Track.Title}"));
            }
            else if (request.Feedback == UserFeedback.AlreadyKnown)
            {
                signals.Add(new TasteSignal(
                    request.UserId,
                    TasteTargetType.Track,
                    trackKey,
                    0.0,
                    TasteSignalSource.RecommendationAlreadyKnown,
                    $"Marked familiar: {recommendation.Track.Title} by {artistName}"));
                signals.Add(new TasteSignal(
                    request.UserId,
                    TasteTargetType.Artist,
                    artistName,
                    0.0,
                    TasteSignalSource.RecommendationAlreadyKnown,
                    $"Marked artist familiar: {artistName}"));
            }

            if (signals.Count > 0)
            {
                await _tasteSignalRepository.AddRangeAsync(signals, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
