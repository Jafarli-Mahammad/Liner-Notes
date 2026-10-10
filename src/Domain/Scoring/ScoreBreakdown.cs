using System.Text;

namespace LinerNotes.Domain.Scoring;

/// <summary>
/// A complete, inspectable, and persistent record of how a recommendation's score was computed.
/// The entire "why this pick" explanation feature reconstructs from this data verbatim.
/// </summary>
public sealed record ScoreBreakdown : ExtensibleScoreRecord
{
    public ScoreSnapshot? Snapshot { get; init; }
    public double FinalScore { get; init; }

    // Tag similarity
    public double TagSimilarityRaw { get; init; }
    public double TagSimilarityScore { get; init; }

    // Popularity penalty (anti-popularity bias mechanic)
    public double PopularityRaw { get; init; }
    public double PopularityPenalty { get; init; }

    // Novelty adjustment
    public double NoveltyRaw { get; init; }
    public double NoveltyBoost { get; init; }

    // Feedback adjustment (penalizes prior explicit dislikes)
    public double FeedbackPenaltyRaw { get; init; }
    public double FeedbackPenalty { get; init; }

    // Matched tags
    public IReadOnlyList<MatchedTagContribution> MatchedTags { get; init; } = Array.Empty<MatchedTagContribution>();

    // Parameterless constructor for persistence/serialization
    public ScoreBreakdown() { }

    public ScoreBreakdown(
        double finalScore,
        double tagSimilarityRaw,
        double tagSimilarityScore,
        double popularityRaw,
        double popularityPenalty,
        double noveltyRaw,
        double noveltyBoost,
        double feedbackPenaltyRaw,
        double feedbackPenalty,
        IReadOnlyList<MatchedTagContribution> matchedTags)
    {
        FinalScore = finalScore;
        TagSimilarityRaw = tagSimilarityRaw;
        TagSimilarityScore = tagSimilarityScore;
        PopularityRaw = popularityRaw;
        PopularityPenalty = popularityPenalty;
        NoveltyRaw = noveltyRaw;
        NoveltyBoost = noveltyBoost;
        FeedbackPenaltyRaw = feedbackPenaltyRaw;
        FeedbackPenalty = feedbackPenalty;
        MatchedTags = matchedTags ?? Array.Empty<MatchedTagContribution>();
    }

    /// <summary>
    /// Reconstructs a deterministic, human-readable explanation strictly from stored numerical data.
    /// Never generates hallucinated text; purely formats verified computed signals.
    /// </summary>
    public string GenerateExplanation()
    {
        if (Snapshot is { } snapshot)
        {
            var tags = string.Join(", ", snapshot.Contributions.OrderByDescending(t => t.NormalizedWeightedContribution)
                .ThenBy(t => t.TagName, StringComparer.Ordinal).Take(3).Select(t => t.TagName));
            return $"Taste overlap: {TagSimilarityRaw:P0}" + (tags.Length == 0 ? "" : $" (shared tags: {tags})") +
                (snapshot.Familiarity ? "; familiar seed artist" : "; artist outside your familiar seeds") +
                $". Final score: {FinalScore:F3}.";
        }
        var sb = new StringBuilder();

        // 1. Tag overlap explanation
        if (MatchedTags.Count > 0)
        {
            var topTags = string.Join(", ", MatchedTags.Take(3).Select(t => t.TagName));
            sb.Append($"Shared tags ({topTags}) with {TagSimilarityRaw:P0} taste overlap");
        }
        else
        {
            sb.Append($"Taste overlap: {TagSimilarityRaw:P0}");
        }

        // 2. Anti-popularity bias note
        if (PopularityRaw < 0.25)
        {
            sb.Append("; underground discovery (low global popularity)");
        }
        else if (PopularityRaw > 0.70)
        {
            sb.Append($"; popular artist (popularity penalty -{PopularityPenalty:F2} applied)");
        }

        // 3. Novelty note
        if (NoveltyRaw >= 0.8)
        {
            sb.Append("; completely new to your library");
        }
        else if (NoveltyRaw <= 0.2)
        {
            sb.Append("; familiar artist in your regular rotation");
        }

        // 4. Feedback penalty note
        if (FeedbackPenaltyRaw > 0)
        {
            sb.Append($"; warning: penalized -{FeedbackPenalty:F2} due to past negative feedback");
        }

        sb.Append($". Final score: {FinalScore:F3}.");

        return sb.ToString();
    }

    public override string ToString() => GenerateExplanation();
}
