using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Common;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;

namespace LinerNotes.Domain.Digest;

/// <summary>
/// An individual recommended track within a weekly digest.
/// Persists the complete, structured ScoreBreakdown to ensure explainability is reconstructable verbatim.
/// </summary>
public sealed class WeeklyRecommendation : BaseEntity, IAuditableEntity
{
    public Guid WeeklyDigestId { get; private set; }
    public Guid UserId { get; private set; }
    public Track Track { get; private set; }
    public int Rank { get; private set; }
    public ScoreBreakdown ScoreBreakdown { get; private set; }
    public UserFeedback Feedback { get; private set; } = UserFeedback.None;
    public string? FeedbackComment { get; private set; }
    public DateTime? FeedbackGivenAt { get; private set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedAt { get; set; }

    // Parameterless constructor for EF Core
    private WeeklyRecommendation()
    {
        Track = null!;
        ScoreBreakdown = null!;
    }

    public WeeklyRecommendation(
        Guid weeklyDigestId,
        Guid userId,
        Track track,
        int rank,
        ScoreBreakdown scoreBreakdown,
        Guid? id = null)
    {
        if (weeklyDigestId == Guid.Empty)
            throw new ArgumentException("WeeklyDigestId cannot be empty.", nameof(weeklyDigestId));
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (rank < 1)
            throw new ArgumentOutOfRangeException(nameof(rank), "Rank must be positive (1-indexed).");

        if (id.HasValue) Id = id.Value;
        WeeklyDigestId = weeklyDigestId;
        UserId = userId;
        Track = track ?? throw new ArgumentNullException(nameof(track));
        Rank = rank;
        ScoreBreakdown = scoreBreakdown ?? throw new ArgumentNullException(nameof(scoreBreakdown));
        CreatedAt = DateTime.UtcNow;
    }

    public void RecordFeedback(UserFeedback feedback, string? comment = null)
    {
        Feedback = feedback;
        FeedbackComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        FeedbackGivenAt = DateTime.UtcNow;
        LastModifiedAt = DateTime.UtcNow;
    }

    public string WhyThisPick() => ScoreBreakdown.GenerateExplanation();

    public override string ToString() => $"#{Rank} {Track} - {Feedback}";
}
