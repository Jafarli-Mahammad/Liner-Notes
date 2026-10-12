using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using Xunit;

namespace LinerNotes.Domain.Tests;

public sealed class WeeklySchedulePolicyTests
{
    [Theory]
    [InlineData("2026-10-12T09:00:00Z", 5, 17, "2026-10-16T17:00:00Z")]
    [InlineData("2026-10-16T17:00:00Z", 5, 17, "2026-10-16T17:00:00Z")]
    [InlineData("2026-10-16T17:00:01Z", 5, 17, "2026-10-23T17:00:00Z")]
    [InlineData("2026-10-12T09:00:00Z", 0, 8, "2026-10-18T08:00:00Z")]
    public void NextUtc_IsTheFirstMatchingSlotAtOrAfterNow(string now, int day, int hour, string expected) =>
        Assert.Equal(DateTime.Parse(expected).ToUniversalTime(), WeeklySchedulePolicy.NextUtc(
            DateTime.Parse(now).ToUniversalTime(), (DigestDeliveryDay)day, hour));

    [Theory]
    [InlineData("2018-12-31T08:00:00Z", "2019-W01")]
    [InlineData("2021-01-01T08:00:00Z", "2020-W53")]
    public void Week_IsIsoWeekAcrossYearBoundary(string due, string expected) =>
        Assert.Equal(expected, WeeklySchedulePolicy.Week(DateTime.Parse(due).ToUniversalTime()).Value);

    [Fact]
    public void Advance_UsesTheClaimedSlotRatherThanTheCurrentClock() =>
        Assert.Equal(new DateTime(2026, 10, 9, 17, 0, 0, DateTimeKind.Utc),
            WeeklySchedulePolicy.Advance(new(2026, 10, 2, 17, 0, 0, DateTimeKind.Utc)));

    [Theory]
    [InlineData(-1, 8)]
    [InlineData(7, 8)]
    [InlineData(0, -1)]
    [InlineData(0, 24)]
    public void InvalidPreferences_AreRejected(int day, int hour) =>
        Assert.Throws<ArgumentException>(() => WeeklySchedulePolicy.NextUtc(DateTime.UtcNow, (DigestDeliveryDay)day, hour));

    [Fact]
    public void NonUtcAndInconsistentSlots_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => WeeklySchedulePolicy.NextUtc(DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified), DigestDeliveryDay.Friday, 17));
        Assert.Throws<ArgumentException>(() => WeeklySchedulePolicy.ValidateSlot(new(2026, 10, 16, 17, 1, 0, DateTimeKind.Utc), DigestDeliveryDay.Friday, 17));
        Assert.Throws<ArgumentException>(() => WeeklySchedulePolicy.ValidateSlot(new(2026, 10, 16, 17, 0, 0, DateTimeKind.Utc), DigestDeliveryDay.Sunday, 17));
    }
}
