using LinerNotes.Worker;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LinerNotes.DataAccess.Tests;
public sealed class LocalWorkerOptionsTests
{
    [Theory]
    [InlineData(null,"Synthetic","2026-W41")]
    [InlineData("capture","Live","2026-W41")]
    [InlineData("schedule","Synthetic","2026-W41")]
    [InlineData("generate","Synthetic","2025-W53")]
    [InlineData("capture","Synthetic","2026-W1")]
    public void ExplicitOneShotOptions_FailClosed(string? action,string origin,string week)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"action",action},{"origin",origin},{"week",week},{"user",Guid.NewGuid().ToString()}}).Build();
        Assert.ThrowsAny<Exception>(()=>LocalWorkerOptions.Parse(config));
    }
    [Fact]
    public void ExplicitCapture_IsAccepted()
    {
        var id=Guid.NewGuid();var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"action","capture"},{"origin","Synthetic"},{"week","2026-W41"},{"user",id.ToString()}}).Build();
        Assert.Equal(id,LocalWorkerOptions.Parse(config).UserId);
    }
}
