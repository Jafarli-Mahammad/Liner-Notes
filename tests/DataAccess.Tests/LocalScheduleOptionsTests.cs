using LinerNotes.Worker;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed class LocalScheduleOptionsTests
{
    private static Dictionary<string, string?> Settings() => new()
    {
        ["action"] = "schedule", ["origin"] = "Synthetic", ["Schedule:Users:0"] = Guid.NewGuid().ToString()
    };

    [Fact]
    public void Defaults_AreOneFiniteSyntheticTick()
    {
        var options = LocalScheduleOptions.Parse(new ConfigurationBuilder().AddInMemoryCollection(Settings()).Build());
        Assert.Single(options.Users); Assert.Equal(1, options.MaxTicks); Assert.Equal(10, options.MaxUsersPerTick);
        Assert.Equal(TimeSpan.FromSeconds(60), options.Interval);
    }

    [Theory]
    [InlineData("origin", "Recorded")]
    [InlineData("origin", "Live")]
    [InlineData("origin", null)]
    [InlineData("Schedule:Users:0", null)]
    [InlineData("Schedule:Users:0", "00000000-0000-0000-0000-000000000000")]
    [InlineData("Schedule:MaxUsersPerTick", "101")]
    [InlineData("Schedule:MaxTicks", "1001")]
    [InlineData("Schedule:MaxTicks", "0")]
    [InlineData("Schedule:IntervalSeconds", "3601")]
    [InlineData("Schedule:IntervalSeconds", "-1")]
    [InlineData("Schedule:IntervalSeconds", "1.5")]
    [InlineData("week", "2026-W42")]
    public void InvalidOrAmbiguousOptions_FailClosed(string key, string? value)
    {
        var settings = Settings(); settings[key] = value;
        Assert.Throws<ArgumentException>(() => LocalScheduleOptions.Parse(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));
    }

    [Fact]
    public void ProcessOptions_IgnoreOsUserButRejectExplicitLegacySelectors()
    {
        var settings = Settings(); settings["user"] = "operating-system-user";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        Assert.Single(LocalScheduleOptions.Parse(configuration, []).Users);
        Assert.Throws<ArgumentException>(() => LocalScheduleOptions.Parse(configuration, ["--user", Guid.NewGuid().ToString()]));
        Assert.Throws<ArgumentException>(() => LocalScheduleOptions.Parse(configuration, ["--week", "2026-W42"]));
    }

    [Fact]
    public void DuplicateOrUnboundedAllowlist_IsRejected()
    {
        var settings = Settings(); settings["Schedule:Users:1"] = settings["Schedule:Users:0"];
        Assert.Throws<ArgumentException>(() => LocalScheduleOptions.Parse(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));
        for (var index = 0; index < 101; index++) settings[$"Schedule:Users:{index}"] = Guid.NewGuid().ToString();
        Assert.Throws<ArgumentException>(() => LocalScheduleOptions.Parse(new ConfigurationBuilder().AddInMemoryCollection(settings).Build()));
    }
}
