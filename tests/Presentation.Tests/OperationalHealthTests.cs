using System.Text.Json;
using LinerNotes.Application.Common.Diagnostics;
using LinerNotes.Presentation.Production;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class OperationalHealthTests
{
    [Fact]
    public void ReadinessRequiresVerifiedStorageDatabaseAndDiskButNotUpstreamAvailability()
    {
        var status=new OperationalStatus(TimeProvider.System);
        Assert.False(OperationalHealth.Ready(status.Snapshot()));
        foreach(var component in new[]{OperationalComponent.Storage,OperationalComponent.Database,OperationalComponent.Disk})
            status.Report(component,true);
        Assert.True(OperationalHealth.Ready(status.Snapshot()));
        status.Report(OperationalComponent.Acquisition,false,"provider_backoff");
        Assert.True(OperationalHealth.Ready(status.Snapshot()));
        status.Report(OperationalComponent.Storage,false,"artifact_inventory_changed");
        Assert.False(OperationalHealth.Ready(status.Snapshot()));
    }

    [Fact]
    public void HealthStateRetainsFailureHistoryAndRejectsPayloadShapedCodes()
    {
        var status=new OperationalStatus(TimeProvider.System);
        status.Report(OperationalComponent.Recovery,false,"private/user/path?token=fixture");
        status.Report(OperationalComponent.Recovery,true);
        var value=status.Snapshot()[OperationalComponent.Recovery];
        Assert.True(value.Healthy);
        Assert.Equal(1,value.Failures);
        Assert.NotNull(value.LastFailure);
        Assert.NotNull(value.LastSuccess);
        Assert.Equal("operation_unavailable",value.FailureCode);
        Assert.DoesNotContain("token",JsonSerializer.Serialize(value));
    }
}
