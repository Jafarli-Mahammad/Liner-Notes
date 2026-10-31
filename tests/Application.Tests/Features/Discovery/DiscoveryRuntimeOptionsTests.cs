using LinerNotes.Application.Features.Discovery;
using Xunit;

namespace LinerNotes.Application.Tests.Features.Discovery;

public sealed class DiscoveryRuntimeOptionsTests
{
    [Fact]
    public void JobAgeAndAcquisitionConcurrencyRemainBounded()
    {
        var options = new DiscoveryRuntimeOptions();
        Assert.Equal(600, options.MaximumJobAgeSeconds);
        Assert.Equal(2, options.AcquisitionConcurrency);
        foreach (var age in new[] { 0, 3601 })
        {
            options.MaximumJobAgeSeconds = age;
            Assert.Throws<ArgumentException>(options.Validate);
        }
        options.MaximumJobAgeSeconds = 600;
        foreach (var concurrency in new[] { 0, 5 })
        {
            options.AcquisitionConcurrency = concurrency;
            Assert.Throws<ArgumentException>(options.Validate);
        }
    }

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
