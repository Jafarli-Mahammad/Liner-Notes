using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;

namespace LinerNotes.Domain.Taste;

public sealed record PositiveFeedbackWeights(double FeedbackTagWeight = 0.5, double MinimumPositiveFeedbackWeight = 0.4);

public sealed record EffectiveTasteInputs(IReadOnlyList<TasteSignal> PositiveSignals,
    IReadOnlySet<string> FamiliarArtists, IReadOnlySet<string> ExcludedAliases)
{
    public static EffectiveTasteInputs From(Guid userId, IReadOnlyList<TasteSignal> signals,
        IReadOnlyList<WeeklyRecommendation> recommendations, PositiveFeedbackWeights config)
    {
        if (!double.IsFinite(config.FeedbackTagWeight) || config.FeedbackTagWeight < 0 ||
            !double.IsFinite(config.MinimumPositiveFeedbackWeight) || config.MinimumPositiveFeedbackWeight is < 0 or > 1)
            throw new ArgumentException("Invalid positive feedback configuration.");
        var positives = signals.Where(s => s.Source is not (TasteSignalSource.RecommendationLike or
            TasteSignalSource.RecommendationDislike or TasteSignalSource.RecommendationAlreadyKnown or TasteSignalSource.RecommendationRating))
            .Where(s => s.Weight > 0 && s.TargetType is TasteTargetType.Artist or TasteTargetType.Tag).ToList();
        if (signals.Any(s => !double.IsFinite(s.Weight))) throw new ArgumentException("Nonfinite taste signal.");
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        var feedback = recommendations.OrderByDescending(r => r.FeedbackGivenAt)
            .ThenByDescending(r => r.Id.ToString("D"), StringComparer.Ordinal).ToArray();

        // Merge identity aliases transitively before choosing the latest track feedback.
        var groups = new List<List<LinerNotes.Domain.Digest.WeeklyRecommendation>>();
        foreach (var recommendation in feedback)
        {
            var aliases = TrackIdentity.Aliases(recommendation.Track);
            var matches = groups.Where(g => g.Any(r => TrackIdentity.Aliases(r.Track).Intersect(aliases, StringComparer.Ordinal).Any())).ToArray();
            var group = new List<LinerNotes.Domain.Digest.WeeklyRecommendation> { recommendation };
            foreach (var match in matches) { group.AddRange(match); groups.Remove(match); }
            groups.Add(group);
        }
        foreach (var group in groups)
        {
            var latest = group.OrderByDescending(r => r.FeedbackGivenAt)
                .ThenByDescending(r => r.Id.ToString("D"), StringComparer.Ordinal).First();
            bool known = group.Any(r => r.Feedback == UserFeedback.AlreadyKnown || signals.Any(s =>
                s.Source == TasteSignalSource.RecommendationAlreadyKnown && s.Context.StartsWith($"rec:{r.Id} - ", StringComparison.Ordinal)));
            if (known || latest.Feedback == UserFeedback.Disliked || latest.Rating is <= 3)
                foreach (var alias in group.SelectMany(r => TrackIdentity.Aliases(r.Track))) excluded.Add(alias);
            if (latest.Feedback == UserFeedback.Liked || latest.Rating is >= 7)
            {
                double weight = latest.Rating.HasValue ? Math.Clamp((latest.Rating.Value - 5) / 5.0, config.MinimumPositiveFeedbackWeight, 1) : 1;
                positives.Add(new(userId, TasteTargetType.Artist, latest.Track.ArtistName, weight,
                    TasteSignalSource.RecommendationLike, $"rec:{latest.Id} - Effective positive feedback", latest.Id));
                foreach (var tag in latest.ScoreBreakdown.MatchedTags.OrderByDescending(t => t.ContributionProduct)
                    .ThenBy(t => t.TagName, StringComparer.Ordinal).Take(3))
                    positives.Add(new(userId, TasteTargetType.Tag, tag.TagName, weight * config.FeedbackTagWeight,
                        TasteSignalSource.RecommendationLike, $"rec:{latest.Id} - Effective positive tag feedback", latest.Id));
            }
        }
        foreach (var signal in signals.Where(s => s.TargetType == TasteTargetType.Track &&
            (s.Source == TasteSignalSource.RecommendationAlreadyKnown ||
             s.Weight < 0 && s.Source is not (TasteSignalSource.RecommendationDislike or TasteSignalSource.RecommendationRating))))
            excluded.Add(signal.NormalizedTargetValue);
        var familiar = positives.Where(s => s.TargetType == TasteTargetType.Artist)
            .Select(s => s.NormalizedTargetValue).ToHashSet(StringComparer.Ordinal);
        return new(positives.Where(s => s.Weight > 0).OrderBy(s => s.TargetType).ThenBy(s => s.NormalizedTargetValue, StringComparer.Ordinal)
            .ThenBy(s => s.Id.ToString("D"), StringComparer.Ordinal).ToArray(), familiar, excluded);
    }
}
