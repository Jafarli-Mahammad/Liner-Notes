using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;

namespace LinerNotes.Domain.Taste;

public sealed record PositiveFeedbackWeights(double FeedbackTagWeight = 0.5,
    double MinimumPositiveFeedbackWeight = 0.4, int MinimumNegativeFeedbackRecordings = 3);

public sealed record EffectiveTasteInputs(IReadOnlyList<TasteSignal> PositiveSignals,
    IReadOnlySet<string> FamiliarArtists, IReadOnlySet<string> ExcludedAliases)
{
    public const string FeedbackPolicy = "durable-artists-recent-tags-v1";
    public IReadOnlyList<FeedbackArtistSupport> FeedbackSupport { get; init; } = [];
    public IReadOnlyDictionary<Guid, Guid> FeedbackRecordings { get; init; } = new Dictionary<Guid, Guid>();

    public static EffectiveTasteInputs From(Guid userId, IReadOnlyList<TasteSignal> signals,
        IReadOnlyList<WeeklyRecommendation> recommendations, PositiveFeedbackWeights config,
        IReadOnlyList<FeedbackObservation>? history = null)
        => Build(userId, signals.Where(s => s.UserId == userId).ToArray(),
            recommendations.Where(r => r.UserId == userId).ToArray(), config,
            history?.Where(r => r.UserId == userId).ToArray());

    public static EffectiveTasteInputs FromProfile(Guid profileId, IReadOnlyList<TasteSignal> signals,
        IReadOnlyList<WeeklyRecommendation> recommendations, PositiveFeedbackWeights config,
        IReadOnlyList<FeedbackObservation>? history = null)
        => Build(profileId, signals.Where(s => s.ProfileId == profileId).ToArray(),
            recommendations.Where(r => r.ProfileId == profileId).ToArray(), config,
            history?.Where(r => r.ProfileId == profileId).ToArray());

    private static EffectiveTasteInputs Build(Guid owner, IReadOnlyList<TasteSignal> signals,
        IReadOnlyList<WeeklyRecommendation> recommendations, PositiveFeedbackWeights config,
        IReadOnlyList<FeedbackObservation>? history)
    {
        if (!double.IsFinite(config.FeedbackTagWeight) || config.FeedbackTagWeight < 0 ||
            !double.IsFinite(config.MinimumPositiveFeedbackWeight) || config.MinimumPositiveFeedbackWeight is < 0 or > 1 ||
            config.MinimumNegativeFeedbackRecordings < 2)
            throw new ArgumentException("Invalid feedback configuration.");
        if (signals.Any(s => !double.IsFinite(s.Weight))) throw new ArgumentException("Nonfinite taste signal.");
        var positives = signals.Where(s => s.Source is not (TasteSignalSource.RecommendationLike or
                TasteSignalSource.RecommendationDislike or TasteSignalSource.RecommendationAlreadyKnown or TasteSignalSource.RecommendationRating))
            .Where(s => s.Weight > 0 && s.TargetType is TasteTargetType.Artist or TasteTargetType.Tag or TasteTargetType.Album or TasteTargetType.Track).ToList();
        var excluded = positives.Where(s => s.TargetType == TasteTargetType.Track && s.CanonicalEntityId.HasValue)
            .Select(s => "mbid:" + s.CanonicalEntityId!.Value.ToString("D")).ToHashSet(StringComparer.Ordinal);
        var observations = (history ?? recommendations.Where(r => r.FeedbackGivenAt != null).Select(FeedbackObservation.From).ToArray()).ToArray();
        var parents = Enumerable.Range(0, observations.Length).ToArray();
        int Root(int i)
        {
            while (parents[i] != i) { parents[i] = parents[parents[i]]; i = parents[i]; }
            return i;
        }
        var aliases = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < observations.Length; i++)
            foreach (var alias in observations[i].Aliases)
                if (aliases.TryGetValue(alias, out var other)) parents[Root(i)] = Root(other);
                else aliases.Add(alias, i);
        var knownIds = signals.Where(s => s.Source == TasteSignalSource.RecommendationAlreadyKnown)
            .Select(s => s.Context.Split(' ', 2)[0]).ToHashSet(StringComparer.Ordinal);
        var latest = new List<FeedbackObservation>();
        foreach (var group in Enumerable.Range(0, observations.Length).GroupBy(Root))
        {
            var current = group.Select(i => observations[i]).OrderByDescending(r => r.ChangedAt)
                .ThenByDescending(r => r.Id.ToString("D"), StringComparer.Ordinal).First();
            // A cleared latest state supersedes previous votes; retained known markers remain separate.
            if (current.Feedback == UserFeedback.AlreadyKnown || IsNegative(current) ||
                group.Any(i => knownIds.Contains($"rec:{observations[i].Id}")))
                foreach (var i in group) excluded.UnionWith(observations[i].Aliases);
            latest.Add(current);
        }
        var receipts = recommendations.ToDictionary(r => r.Id);
        var support = new List<FeedbackArtistSupport>();
        var recordings = new Dictionary<Guid, Guid>();
        var learnedTags = new Dictionary<string, TasteSignal>(StringComparer.Ordinal);
        foreach (var artist in latest.GroupBy(r => r.ArtistKey).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var likes = artist.Where(IsPositive).OrderByDescending(r => Weight(r, config))
                .ThenByDescending(r => r.ChangedAt).ThenBy(r => r.Id).ToArray();
            var negatives = artist.Count(IsNegative);
            var attenuation = negatives >= config.MinimumNegativeFeedbackRecordings && negatives > likes.Length;
            var factor = attenuation ? Math.Max(config.MinimumPositiveFeedbackWeight, (double)likes.Length / (likes.Length + negatives)) : 1;
            var weight = likes.Length == 0 ? 0 : Weight(likes[0], config) * factor;
            support.Add(new(artist.Key, likes.Length, negatives, likes.Count(r => receipts.ContainsKey(r.Id)), weight, attenuation && likes.Length > 0));
            if (likes.Length == 0) continue;
            var representative = likes[0];
            positives.Add(new TasteSignal(owner, TasteTargetType.Artist, representative.Artist, weight,
                TasteSignalSource.RecommendationLike,
                $"rec:{representative.Id} - Learned artist evidence: {likes.Length} positive, {negatives} negative independent recordings; factor {factor.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}.", representative.Id));
            if (Guid.TryParse(representative.Mbid, out var recording) && recording != Guid.Empty) recordings[representative.Id] = recording;
            foreach (var like in likes)
                if (receipts.TryGetValue(like.Id, out var receipt))
                    foreach (var tag in receipt.ScoreBreakdown.MatchedTags.OrderByDescending(t => t.ContributionProduct)
                                 .ThenBy(t => t.TagName, StringComparer.Ordinal).Take(3))
                    {
                        var signal = new TasteSignal(owner, TasteTargetType.Tag, tag.TagName, Weight(like, config) * config.FeedbackTagWeight * factor,
                            TasteSignalSource.RecommendationLike, $"rec:{like.Id} - Recent matched-tag evidence; learned artist factor {factor.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}.", like.Id);
                        if (!learnedTags.TryGetValue(signal.NormalizedTargetValue, out var previous) || signal.Weight > previous.Weight ||
                            signal.Weight == previous.Weight && string.CompareOrdinal(signal.Id.ToString("D"), previous.Id.ToString("D")) < 0)
                            learnedTags[signal.NormalizedTargetValue] = signal;
                    }
        }
        foreach (var signal in signals.Where(s => s.TargetType == TasteTargetType.Track &&
                     (s.Source == TasteSignalSource.RecommendationAlreadyKnown ||
                      s.Weight < 0 && s.Source is not (TasteSignalSource.RecommendationDislike or TasteSignalSource.RecommendationRating))))
            excluded.Add(signal.NormalizedTargetValue);
        positives.AddRange(learnedTags.Values);
        var familiar = positives.Where(s => s.TargetType == TasteTargetType.Artist)
            .Select(s => s.NormalizedTargetValue).ToHashSet(StringComparer.Ordinal);
        return new(positives.Where(s => s.Weight > 0).OrderBy(s => s.TargetType)
            .ThenBy(s => s.NormalizedTargetValue, StringComparer.Ordinal).ThenBy(s => s.Id.ToString("D"), StringComparer.Ordinal).ToArray(), familiar, excluded)
            { FeedbackSupport = support, FeedbackRecordings = recordings };
    }

    private static bool IsPositive(FeedbackObservation r) => r.Feedback == UserFeedback.Liked || r.Rating >= 7;
    private static bool IsNegative(FeedbackObservation r) => r.Feedback == UserFeedback.Disliked || r.Rating <= 3;
    private static double Weight(FeedbackObservation r, PositiveFeedbackWeights config) => r.Rating.HasValue
        ? Math.Clamp((r.Rating.Value - 5) / 5.0, config.MinimumPositiveFeedbackWeight, 1) : 1;
}
