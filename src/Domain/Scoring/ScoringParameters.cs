namespace LinerNotes.Domain.Scoring;

/// <summary>
/// Configurable weighting parameters for the deterministic scoring engine.
/// Weights are configuration, never hard-coded, allowing calibration against real feedback.
/// </summary>
public sealed record ScoringParameters
{
    public double TagSimilarityWeight { get; init; } = 1.0;
    public double PopularityPenaltyWeight { get; init; } = 0.3;
    public double NoveltyBoostWeight { get; init; } = 0.2;
    public double FeedbackPenaltyWeight { get; init; } = 1.0;

    public static ScoringParameters Default => new();

    public ScoringParameters() { }

    public ScoringParameters(
        double tagSimilarityWeight,
        double popularityPenaltyWeight,
        double noveltyBoostWeight,
        double feedbackPenaltyWeight)
    {
        if (tagSimilarityWeight < 0)
            throw new ArgumentOutOfRangeException(nameof(tagSimilarityWeight), "Tag similarity weight must be non-negative.");
        if (popularityPenaltyWeight < 0)
            throw new ArgumentOutOfRangeException(nameof(popularityPenaltyWeight), "Popularity penalty weight must be non-negative.");
        if (noveltyBoostWeight < 0)
            throw new ArgumentOutOfRangeException(nameof(noveltyBoostWeight), "Novelty boost weight must be non-negative.");
        if (feedbackPenaltyWeight < 0)
            throw new ArgumentOutOfRangeException(nameof(feedbackPenaltyWeight), "Feedback penalty weight must be non-negative.");

        TagSimilarityWeight = tagSimilarityWeight;
        PopularityPenaltyWeight = popularityPenaltyWeight;
        NoveltyBoostWeight = noveltyBoostWeight;
        FeedbackPenaltyWeight = feedbackPenaltyWeight;
    }
}
