using LinerNotes.Domain.Discovery;
using Xunit;
namespace LinerNotes.Domain.Tests;
public sealed class DiscoveryJobLeaseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    [Fact]
    public void ExpiredLeaseCanBeReclaimedButOldWorkerCannotRenewOrPublish()
    {
        var job = new DiscoveryJob(Guid.NewGuid(), "retry", new string('a', 64), DiscoveryJobKind.Lookup, 0, "{\"query\":\"synthetic\"}", Now);
        Guid old = Guid.NewGuid(), current = Guid.NewGuid();
        job.AcquireLease(old, Now, TimeSpan.FromSeconds(10), 3);
        Assert.Throws<InvalidOperationException>(() => job.AcquireLease(current, Now.AddSeconds(9), TimeSpan.FromSeconds(10), 3));
        job.AcquireLease(current, Now.AddSeconds(10), TimeSpan.FromSeconds(10), 3);
        Assert.Throws<InvalidOperationException>(() => job.Complete(old, Now.AddSeconds(11), null, "{}"));
        Assert.Throws<InvalidOperationException>(() => job.RenewLease(old, Now.AddSeconds(11), TimeSpan.FromSeconds(10), "preparing"));
        job.Complete(current, Now.AddSeconds(11), null, "{}");
        Assert.Equal(DiscoveryJobState.Completed, job.State);
        Assert.Equal("{}", job.PayloadJson);
        Assert.Null(job.LeaseToken);
        Assert.Equal(2, job.Attempts);
    }
    [Fact]
    public void RetryWaitAndAttemptBoundAreEnforcedAndTerminalPayloadIsCleared()
    {
        var job = new DiscoveryJob(Guid.NewGuid(), "retry", new string('a', 64), DiscoveryJobKind.Generation, 0, "{\"input\":1}", Now);
        Guid token = Guid.NewGuid();
        job.AcquireLease(token, Now, TimeSpan.FromSeconds(10), 2);
        job.Fail(token, Now.AddSeconds(1), "transport_error", true, TimeSpan.FromSeconds(5), 2);
        Assert.Equal(DiscoveryJobState.Retryable, job.State);
        Assert.NotEqual("{}", job.PayloadJson);
        Assert.Throws<InvalidOperationException>(() => job.AcquireLease(Guid.NewGuid(), Now.AddSeconds(5), TimeSpan.FromSeconds(10), 2));
        token = Guid.NewGuid();
        job.AcquireLease(token, Now.AddSeconds(6), TimeSpan.FromSeconds(10), 2);
        job.Fail(token, Now.AddSeconds(7), "transport_error", true, TimeSpan.FromSeconds(5), 2);
        Assert.Equal(DiscoveryJobState.Failed, job.State);
        Assert.Equal("{}", job.PayloadJson);
        Assert.Throws<InvalidOperationException>(() => job.AcquireLease(Guid.NewGuid(), Now.AddSeconds(20), TimeSpan.FromSeconds(10), 2));
    }
}
