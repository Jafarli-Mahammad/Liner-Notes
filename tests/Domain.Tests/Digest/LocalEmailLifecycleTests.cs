using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using Xunit;

namespace LinerNotes.Domain.Tests.Digest;

public sealed class LocalEmailLifecycleTests
{
    [Fact]
    public void OptOut_IsIdempotentUtcAndKeepsAccount()
    {
        var user = new User("local@example.test");
        var time = DateTime.UtcNow;
        user.UnsubscribeEmail(time);
        user.UnsubscribeEmail(time.AddDays(1));
        Assert.Equal(time, user.EmailUnsubscribedAtUtc);
        Assert.False(user.IsDeleted);
        Assert.Throws<ArgumentException>(() => user.UnsubscribeEmail(DateTime.Now));
    }

    [Fact]
    public void Capture_IsDistinctFromSentAndCannotBeOverwritten()
    {
        var digest = new WeeklyDigest(Guid.NewGuid(), new IsoWeek(2026, 41), DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
        digest.MarkLocalCaptured();
        digest.MarkLocalCaptured();
        Assert.Equal(4, (int)digest.Status);
        Assert.Null(digest.SentAt);
        Assert.Throws<InvalidOperationException>(() => digest.MarkInProgress());
        Assert.Throws<InvalidOperationException>(() => digest.MarkFailed("failure"));
        Assert.Throws<InvalidOperationException>(() => digest.MarkSent(DateTime.UtcNow));
        var sent = new WeeklyDigest(Guid.NewGuid(), new IsoWeek(2026, 41), DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
        sent.MarkSent(DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => sent.MarkLocalCaptured());
    }
}
