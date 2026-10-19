using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Common;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;

namespace LinerNotes.Domain.Digest;

/// <summary>
/// An individual recommended track within a weekly digest.
/// Persists the complete, structured ScoreBreakdown to ensure explainability is reconstructable verbatim.
/// </summary>
public sealed class WeeklyRecommendation : AuditableEntity
{
    public Guid? WeeklyDigestId { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid ProfileId { get; private set; }
    public Guid BatchId { get; private set; }
    public Track Track { get; private set; }
    public int Rank { get; private set; }
    public ScoreBreakdown ScoreBreakdown { get; private set; }
    public UserFeedback Feedback { get; private set; } = UserFeedback.None;
    public int? Rating { get; private set; }
    public string? FeedbackComment { get; private set; }
    public DateTime? FeedbackGivenAt { get; private set; }

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
        ProfileId = userId;
        BatchId = weeklyDigestId;
        Track = track ?? throw new ArgumentNullException(nameof(track));
        Rank = rank;
        ScoreBreakdown = scoreBreakdown ?? throw new ArgumentNullException(nameof(scoreBreakdown));
        CreatedAt = DateTime.UtcNow;
    }

    public static WeeklyRecommendation ForDiscovery(Guid profileId, Guid batchId, Guid? userId, Track track, int rank, ScoreBreakdown breakdown)
    {
        if (profileId == Guid.Empty || batchId == Guid.Empty || userId == Guid.Empty) throw new ArgumentException("Invalid owner.");
        return new WeeklyRecommendation(batchId, profileId, track, rank, breakdown) { UserId = userId, WeeklyDigestId = null };
    }
    public void BindRegisteredProfile(Guid userId, Guid profileId)
    {
        if (UserId != userId || profileId == Guid.Empty) throw new ArgumentException("Invalid profile binding.");
        ProfileId = profileId;
    }
    public void RecordFeedback(UserFeedback feedback, string? comment = null, int? rating = null)
    {
        if (!Enum.IsDefined(feedback))
            throw new ArgumentOutOfRangeException(nameof(feedback), "A defined feedback value is required.");
        if (comment?.Length > 1000)
            throw new ArgumentException("Feedback comment cannot exceed 1000 characters.", nameof(comment));
        if (rating.HasValue && (rating.Value < 1 || rating.Value > 10))
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 10.");
        if (feedback == UserFeedback.Liked && rating is <= 3 ||
            feedback == UserFeedback.Disliked && rating is >= 7)
            throw new ArgumentException("The rating contradicts the selected feedback.", nameof(rating));

        Feedback = feedback;
        Rating = rating;
        FeedbackComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        FeedbackGivenAt = DateTime.UtcNow;
        LastModifiedAt = DateTime.UtcNow;
    }

    public string WhyThisPick() => ScoreBreakdown.GenerateExplanation();

    public override string ToString() => $"#{Rank} {Track} - {Feedback}";
}
