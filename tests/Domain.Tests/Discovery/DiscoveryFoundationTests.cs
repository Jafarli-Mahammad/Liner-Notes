using LinerNotes.Domain.Discovery;
using Xunit;

namespace LinerNotes.Domain.Tests.Discovery;

public sealed class DiscoveryFoundationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GuestExpiryIsEnforcedBeforeTouchOrClaimAndActivityWritesCanBeThrottled()
    {
        var profile = ListenerProfile.Guest(Now, TimeSpan.FromDays(7));
        Assert.True(profile.IsGuest);
        Assert.Equal(Now.AddDays(7), profile.ExpiresAtUtc);
        Assert.False(profile.Touch(Now.AddMinutes(5), TimeSpan.FromDays(7), TimeSpan.FromHours(1)));
        Assert.Equal(Now, profile.LastActiveAtUtc);
        Assert.True(profile.Touch(Now.AddHours(2), TimeSpan.FromDays(7), TimeSpan.FromHours(1)));
        var expiry = Now.AddHours(2).AddDays(7);
        Assert.Equal(expiry, profile.ExpiresAtUtc);
        Assert.Throws<InvalidOperationException>(() => profile.Touch(expiry, TimeSpan.FromDays(7), TimeSpan.Zero));
        Assert.Throws<InvalidOperationException>(() => profile.Attach(Guid.NewGuid(), expiry));
    }

    [Fact]
    public void AttachmentPreservesProfileIdentityAndCannotOverwriteAnExistingAccount()
    {
        var profile = ListenerProfile.Guest(Now, TimeSpan.FromDays(7));
        var id = profile.Id;
        var account = Guid.NewGuid();
        profile.Attach(account, Now.AddHours(1));
        profile.Attach(account, Now.AddHours(2));
        Assert.Equal(id, profile.Id);
        Assert.Equal(account, profile.UserId);
        Assert.False(profile.IsGuest);
        Assert.Null(profile.ExpiresAtUtc);
        Assert.Throws<InvalidOperationException>(() => profile.Attach(Guid.NewGuid(), Now.AddHours(3)));
    }

    [Fact]
    public void RegisteredMappingAndRevisionDoNotDependOnCalendarWeeks()
    {
        var account = Guid.NewGuid();
        var profile = ListenerProfile.Registered(account, Now);
        Assert.Equal(account, profile.Id);
        Assert.Equal(account, profile.UserId);
        Assert.Null(profile.ExpiresAtUtc);
        profile.ChangeInputs(0);
        Assert.Equal(1, profile.Revision);
        Assert.Throws<InvalidOperationException>(() => profile.ChangeInputs(0));
        Assert.Throws<ArgumentException>(() => ListenerProfile.Registered(Guid.Empty, Now));
        Assert.Throws<ArgumentException>(() => ListenerProfile.Guest(Now.ToOffset(TimeSpan.FromHours(1)), TimeSpan.FromDays(7)));
    }

    [Fact]
    public void DistinctOnDemandBatchesCanExistOnTheSameDateAndCompletionIsMonotonic()
    {
        var owner = Guid.NewGuid();
        var first = new DiscoveryBatch(owner, "first", DiscoveryBatchKind.Initial, 1, Now);
        var next = new DiscoveryBatch(owner, "next", DiscoveryBatchKind.OnDemand, 1, Now.AddMinutes(1));
        Assert.NotEqual(first.Id, next.Id);
        Assert.Equal(owner, next.ProfileId);
        first.Complete(Now.AddSeconds(1));
        Assert.Equal(Now.AddSeconds(1), first.CompletedAtUtc);
        Assert.Throws<ArgumentException>(() => next.Complete(Now));
        Assert.Throws<ArgumentException>(() => new DiscoveryBatch(Guid.Empty, "first", DiscoveryBatchKind.Initial, 0, Now));
    }

    [Fact]
    public void PendingJobsHaveBoundedIdentityPayloadAndNoLeaseOrResult()
    {
        var job = new DiscoveryJob(Guid.NewGuid(), "request-1", new string('a', 64),
            DiscoveryJobKind.Generation, 0, "{}", Now);
        Assert.Equal(DiscoveryJobState.Pending, job.State);
        Assert.Equal(Now, job.NextAttemptAtUtc);
        Assert.Equal(0, job.Attempts);
        Assert.Null(job.LeaseToken);
        Assert.Null(job.ResultBatchId);
        Assert.Throws<ArgumentException>(() => new DiscoveryJob(Guid.NewGuid(), "request-1", "invalid",
            DiscoveryJobKind.Generation, 0, "{}", Now));
        Assert.Throws<ArgumentException>(() => new DiscoveryJob(Guid.NewGuid(), new string('x', 81), new string('a', 64),
            DiscoveryJobKind.Lookup, 0, "{}", Now));
        Assert.Throws<ArgumentException>(() => new DiscoveryJob(Guid.NewGuid(), "request-1", new string('a', 64),
            DiscoveryJobKind.Lookup, 0, new string('夜', 6000), Now));
    }
}
