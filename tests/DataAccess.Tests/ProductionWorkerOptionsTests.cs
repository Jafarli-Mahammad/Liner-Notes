using LinerNotes.Worker;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed class ProductionWorkerOptionsTests
{
    private static Dictionary<string,string?> Valid() => new()
    {
        ["action"]="generate",["user"]=Guid.NewGuid().ToString(),["week"]="2026-W44",["origin"]="Live",
        ["Generation:Origin"]="Live",["Generation:Provider"]="listenbrainz",["Generation:LiveEnabled"]="true",
        ["ListenBrainz:Mode"]="LiveOnly",["ListenBrainz:LiveUseApproved"]="true",
        ["DiscoveryRuntime:EnforceStorageAdmission"]="true"
    };

    [Fact]
    public void ExplicitProductionGenerationIsBounded()
    {
        var options=ProductionWorkerOptions.Parse(new ConfigurationBuilder().AddInMemoryCollection(Valid()).Build());
        Assert.Equal(TimeSpan.FromSeconds(180),options.Timeout);
        Assert.Equal("generate",options.Request.Action);
    }

    [Theory]
    [InlineData("action","capture")]
    [InlineData("action","schedule")]
    [InlineData("origin","Synthetic")]
    [InlineData("origin","Recorded")]
    [InlineData("ListenBrainz:LiveUseApproved","false")]
    [InlineData("LocalEmail:Enabled","true")]
    [InlineData("DiscoveryRuntime:EnforceStorageAdmission","false")]
    [InlineData("ProductionWorker:TimeoutSeconds","0")]
    public void ProductionDoesNotEnableMailFixtureOrUnboundedWork(string key,string value)
    {
        var config=Valid(); config[key]=value;
        Assert.Throws<ArgumentException>(()=>ProductionWorkerOptions.Parse(new ConfigurationBuilder().AddInMemoryCollection(config).Build()));
    }
}
