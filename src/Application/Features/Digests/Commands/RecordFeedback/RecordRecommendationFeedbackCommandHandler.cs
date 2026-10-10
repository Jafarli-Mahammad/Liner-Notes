using LinerNotes.Application.Common.Exceptions;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;
using LinerNotes.Application.Common.Models.Recommendation;
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
    private readonly GenerationConfiguration _configuration;

    public RecordRecommendationFeedbackCommandHandler(
        IWeeklyDigestRepository weeklyDigestRepository,
        IUnitOfWork unitOfWork,
        ITasteSignalRepository? tasteSignalRepository = null,
        GenerationConfiguration? configuration = null)
    {
        _weeklyDigestRepository = weeklyDigestRepository;
        _unitOfWork = unitOfWork;
        _tasteSignalRepository = tasteSignalRepository;
        _configuration = configuration ?? new GenerationConfiguration();
    }

    public async Task<bool> Handle(
        RecordRecommendationFeedbackCommand request,
        CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
        var recommendation = await _weeklyDigestRepository.GetRecommendationForUpdateAsync(
            request.RecommendationId,
            request.UserId,
            cancellationToken);

        if (recommendation is null)
        {
            throw new NotFoundException(nameof(WeeklyRecommendation), request.RecommendationId);
        }

        var previousSignals = _tasteSignalRepository is null ? [] :
            await _tasteSignalRepository.GetByUserIdAsync(request.UserId, cancellationToken) ?? [];
        bool clearFamiliarity = request.Feedback == UserFeedback.None && request.Rating is null;
        bool familiar = !clearFamiliarity && (request.Feedback == UserFeedback.AlreadyKnown ||
            recommendation.Feedback == UserFeedback.AlreadyKnown || previousSignals.Any(s =>
                s.Source == TasteSignalSource.RecommendationAlreadyKnown &&
                s.Context.StartsWith($"rec:{recommendation.Id} - ", StringComparison.Ordinal)));
        int? rating = request.Rating ?? (request.Feedback == UserFeedback.AlreadyKnown ? recommendation.Rating : null);
        var feedback = familiar && request.Feedback == UserFeedback.None && rating.HasValue
            ? UserFeedback.AlreadyKnown : request.Feedback;
        recommendation.RecordFeedback(feedback, request.Comment, rating);
        _weeklyDigestRepository.UpdateRecommendation(recommendation);

        if (_tasteSignalRepository is not null)
        {
            // Idempotent signal replacement: purge existing signals for this recommendation to prevent signal accumulation or contradiction
            var contextPrefix = $"rec:{recommendation.Id}";
            await _tasteSignalRepository.DeleteSignalsByContextPrefixAsync(request.UserId, contextPrefix, cancellationToken);

            var signals = new List<TasteSignal>();
            var artistName = recommendation.Track.ArtistName;
            var trackKey = recommendation.Track.TrackKey;

            if (familiar)
            {
                signals.Add(new TasteSignal(
                    request.UserId,
                    TasteTargetType.Track,
                    trackKey,
                    0.0,
                    TasteSignalSource.RecommendationAlreadyKnown,
                    $"{contextPrefix} - Marked familiar: {recommendation.Track.Title} by {artistName}"));
            }

            if (request.Feedback == UserFeedback.Liked || rating is >= 7)
            {
                double weight = rating.HasValue ? Math.Clamp((rating.Value - 5) / 5.0, _configuration.MinimumPositiveFeedbackWeight, 1.0) : 1.0;
                signals.Add(new TasteSignal(
                    request.UserId,
                    TasteTargetType.Artist,
                    artistName,
                    weight,
                    TasteSignalSource.RecommendationLike,
                    $"{contextPrefix} - Liked recommendation #{recommendation.Rank}: {recommendation.Track.Title}"));

                if (recommendation.ScoreBreakdown?.MatchedTags is not null)
                {
                    foreach (var matchedTag in recommendation.ScoreBreakdown.MatchedTags.OrderByDescending(t => t.ContributionProduct)
                        .ThenBy(t => t.TagName, StringComparer.Ordinal).Take(3))
                    {
                        signals.Add(new TasteSignal(
                            request.UserId,
                            TasteTargetType.Tag,
                            matchedTag.TagName,
                            weight * _configuration.FeedbackTagWeight,
                            TasteSignalSource.RecommendationLike,
                            $"{contextPrefix} - Nudged tag from liked recommendation #{recommendation.Rank}"));
                    }
                }
            }
            else if (request.Feedback == UserFeedback.Disliked || rating is <= 3)
            {
                double penalty = rating.HasValue ? Math.Clamp((5 - rating.Value) / 5.0, _configuration.MinimumPositiveFeedbackWeight, 1.0) : 1.0;
                signals.Add(new TasteSignal(
                    request.UserId,
                    TasteTargetType.Track,
                    trackKey,
                    -penalty,
                    TasteSignalSource.RecommendationDislike,
                    $"{contextPrefix} - Disliked recommendation #{recommendation.Rank}: {recommendation.Track.Title}"));
            }
            if (signals.Count > 0)
            {
                await _tasteSignalRepository.AddRangeAsync(signals, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
        }, cancellationToken);
    }
}
