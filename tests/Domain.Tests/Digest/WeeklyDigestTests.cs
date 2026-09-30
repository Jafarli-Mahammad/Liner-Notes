using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using Xunit;

namespace LinerNotes.Domain.Tests.Digest;

public sealed class WeeklyDigestTests
{
    [Fact]
    public void WeeklyDigest_AddRecommendation_TracksRankAndScoreBreakdown()
    {
        var digest = WeeklyDigest.Create(
            userId: Guid.NewGuid(),
            week: IsoWeek.From(2026, 40),
            weekStartDate: new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc),
            weekEndDate: new DateTime(2026, 10, 4, 23, 59, 59, DateTimeKind.Utc));

        var track = Track.Create("In the Shadow of Our Pale Companion", "Agalloch", "The Mantle");
        var breakdown = new ScoreBreakdown(
            finalScore: 0.88,
            tagSimilarityRaw: 0.90,
            tagSimilarityScore: 0.90,
            popularityRaw: 0.15,
            popularityPenalty: 0.045,
            noveltyRaw: 1.0,
            noveltyBoost: 0.20,
            feedbackPenaltyRaw: 0.0,
            feedbackPenalty: 0.0,
            matchedTags: new List<MatchedTagContribution>
            {
                new("atmospheric black metal", 1.0, 0.9, 0.9)
            });

        var rec = digest.AddRecommendation(track, breakdown, rank: 1);

        Assert.Single(digest.Recommendations);
        Assert.Equal(1, rec.Rank);
        Assert.Equal(0.88, rec.ScoreBreakdown.FinalScore);
        Assert.Equal(UserFeedback.None, rec.Feedback);
    }

    [Fact]
    public void WeeklyDigest_AddDuplicateTrack_ThrowsInvalidOperationException()
    {
        var digest = WeeklyDigest.Create(
            userId: Guid.NewGuid(),
            week: IsoWeek.From(2026, 40),
            weekStartDate: new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc),
            weekEndDate: new DateTime(2026, 10, 4, 23, 59, 59, DateTimeKind.Utc));

        var track = Track.Create("In the Shadow of Our Pale Companion", "Agalloch", "The Mantle");
        var breakdown = new ScoreBreakdown();

        digest.AddRecommendation(track, breakdown, rank: 1);

        Assert.Throws<InvalidOperationException>(() =>
            digest.AddRecommendation(track, breakdown, rank: 2));
    }

    [Fact]
    public void WeeklyDigest_LifecycleStateTransitions_WorkCorrectly()
    {
        var digest = WeeklyDigest.Create(
            userId: Guid.NewGuid(),
            week: IsoWeek.From(2026, 40),
            weekStartDate: DateTime.UtcNow.AddDays(-7),
            weekEndDate: DateTime.UtcNow);

        Assert.Equal(DigestStatus.Pending, digest.Status);

        digest.MarkInProgress();
        Assert.Equal(DigestStatus.InProgress, digest.Status);

        var sentTime = DateTime.UtcNow;
        digest.MarkSent(sentTime);
        Assert.Equal(DigestStatus.Sent, digest.Status);
        Assert.Equal(sentTime, digest.SentAt);

        // Cannot move back to InProgress once Sent
        Assert.Throws<InvalidOperationException>(() => digest.MarkInProgress());
    }

    [Fact]
    public void WeeklyRecommendation_RecordFeedback_UpdatesSentimentAndTimestamp()
    {
        var digestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var track = Track.Create("Song A", "Band B");
        var breakdown = new ScoreBreakdown();

        var rec = new WeeklyRecommendation(digestId, userId, track, 1, breakdown);

        rec.RecordFeedback(UserFeedback.Liked, "Loved the atmosphere!");

        Assert.Equal(UserFeedback.Liked, rec.Feedback);
        Assert.Equal("Loved the atmosphere!", rec.FeedbackComment);
        Assert.NotNull(rec.FeedbackGivenAt);
    }

    [Fact]
    public void IsoWeek_FormatAndParse_PreservesExactWeek()
    {
        var week = IsoWeek.From(2026, 40);

        Assert.Equal("2026-W40", week.Value);
        Assert.Equal("2026-W40", week.ToString());

        var parsed = IsoWeek.Parse("2026-W40");
        Assert.Equal(week, parsed);
    }
}
