using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Common;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;

namespace LinerNotes.Domain.Digest;

/// <summary>
/// A personalized weekly music discovery digest for a subscriber.
/// Enforces idempotent weekly batch delivery: a user cannot receive multiple digests for the same ISO week.
/// </summary>
public sealed class WeeklyDigest : AuditableEntity
{
    private readonly List<WeeklyRecommendation> _recommendations = new();

    public Guid UserId { get; private set; }
    public IsoWeek Week { get; private set; }
    public string WeekIdentifier => Week.Value;
    public DateTime WeekStartDate { get; private set; }
    public DateTime WeekEndDate { get; private set; }
    public DigestStatus Status { get; private set; } = DigestStatus.Pending;
    public DateTime? SentAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    public IReadOnlyCollection<WeeklyRecommendation> Recommendations => _recommendations.AsReadOnly();

    // Parameterless constructor for EF Core
    private WeeklyDigest()
    {
        Week = new IsoWeek(2026, 1);
    }

    public WeeklyDigest(
        Guid userId,
        IsoWeek week,
        DateTime weekStartDate,
        DateTime weekEndDate,
        Guid? id = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        ArgumentNullException.ThrowIfNull(week);

        if (id.HasValue) Id = id.Value;
        UserId = userId;
        Week = week;
        WeekStartDate = weekStartDate;
        WeekEndDate = weekEndDate;
        Status = DigestStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public static WeeklyDigest Create(
        Guid userId,
        IsoWeek week,
        DateTime weekStartDate,
        DateTime weekEndDate) =>
        new(userId, week, weekStartDate, weekEndDate);

    public WeeklyRecommendation AddRecommendation(Track track, ScoreBreakdown scoreBreakdown, int rank)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(scoreBreakdown);

        if (_recommendations.Any(r => r.Track.TrackKey == track.TrackKey))
            throw new InvalidOperationException($"Track '{track.Title}' by '{track.ArtistName}' is already recommended in this digest.");

        var recommendation = new WeeklyRecommendation(
            weeklyDigestId: Id,
            userId: UserId,
            track: track,
            rank: rank,
            scoreBreakdown: scoreBreakdown);

        _recommendations.Add(recommendation);
        LastModifiedAt = DateTime.UtcNow;

        return recommendation;
    }

    public void MarkInProgress()
    {
        if (Status == DigestStatus.Sent)
            throw new InvalidOperationException("Cannot set a sent digest to InProgress.");

        Status = DigestStatus.InProgress;
        ErrorMessage = null;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void MarkSent(DateTime sentAtUtc)
    {
        Status = DigestStatus.Sent;
        SentAt = sentAtUtc;
        ErrorMessage = null;
        LastModifiedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = DigestStatus.Failed;
        ErrorMessage = errorMessage;
        LastModifiedAt = DateTime.UtcNow;
    }

    public override string ToString() => $"Digest {Week} for User {UserId} ({Status})";
}
