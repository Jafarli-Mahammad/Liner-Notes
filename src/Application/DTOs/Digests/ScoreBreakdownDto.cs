using LinerNotes.Domain.Scoring;

namespace LinerNotes.Application.DTOs.Digests;

/// <summary>
/// Individual tag contribution to a recommendation's similarity score.
/// </summary>
public record MatchedTagContributionDto(
    string TagName,
    double CandidateTagWeight,
    double UserTasteWeight,
    double ContributionProduct) : ExtensibleScoreRecord;

/// <summary>
/// Fully auditable, transparent score breakdown explaining why a pick was selected.
/// </summary>
public record ScoreBreakdownDto(
    double FinalScore,
    double TagSimilarityScore,
    double PopularityPenalty,
    double NoveltyBoost,
    double FeedbackPenalty,
    IReadOnlyList<MatchedTagContributionDto> MatchedTags,
    ScoreSnapshot? Snapshot = null,
    double TagSimilarityRaw = 0,
    double PopularityRaw = 0,
    double NoveltyRaw = 0,
    double FeedbackPenaltyRaw = 0) : ExtensibleScoreRecord;
