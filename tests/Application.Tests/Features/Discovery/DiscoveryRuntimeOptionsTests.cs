using LinerNotes.Application.Features.Discovery;
using Xunit;

namespace LinerNotes.Application.Tests.Features.Discovery;

public sealed class DiscoveryRuntimeOptionsTests
{
    [Fact]
    public void LookupPacingIsIndependentFromBatchCooldownAndRemainsBounded()
    {
        var options = new DiscoveryRuntimeOptions();
        options.Validate();
        Assert.Equal(30, options.CooldownSeconds);
        Assert.Equal(1, options.LookupCooldownSeconds);
        foreach (var invalid in new[] { 0, 61 })
        {
            options.LookupCooldownSeconds = invalid;
            Assert.Throws<ArgumentException>(options.Validate);
        }
    }
}
