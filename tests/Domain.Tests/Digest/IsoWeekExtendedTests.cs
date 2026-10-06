using LinerNotes.Domain.Digest;
using Xunit;

namespace LinerNotes.Domain.Tests.Digest;

public sealed class IsoWeekExtendedTests
{
    [Theory]
    [InlineData(1899, 1)]
    [InlineData(2201, 1)]
    public void IsoWeek_Constructor_YearOutOfRange_ShouldThrowArgumentOutOfRangeException(int year, int week)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IsoWeek(year, week));
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 54)]
    [InlineData(2026, -1)]
    public void IsoWeek_Constructor_WeekOutOfRange_ShouldThrowArgumentOutOfRangeException(int year, int week)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new IsoWeek(year, week));
    }

    [Fact]
    public void IsoWeek_Comparison_ShouldOrderChronologically()
    {
        var w1 = IsoWeek.From(2025, 52);
        var w2 = IsoWeek.From(2026, 1);
        var w3 = IsoWeek.From(2026, 2);

        Assert.True(w1.CompareTo(w2) < 0);
        Assert.True(w2.CompareTo(w3) < 0);
        Assert.True(w3.CompareTo(w1) > 0);
        Assert.True(w2.CompareTo(w1) > 0);
        Assert.Equal(0, w2.CompareTo(IsoWeek.From(2026, 1)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsoWeek_Parse_Empty_ShouldThrowArgumentException(string? input)
    {
        Assert.Throws<ArgumentException>(() => IsoWeek.Parse(input!));
    }

    [Theory]
    [InlineData("2026")]
    [InlineData("2026-40")]
    [InlineData("invalid-W01")]
    [InlineData("2026-Wabc")]
    public void IsoWeek_Parse_InvalidFormat_ShouldThrowFormatException(string input)
    {
        Assert.Throws<FormatException>(() => IsoWeek.Parse(input));
    }

    [Fact]
    public void IsoWeek_FromDate_CalculatesCorrectWeek()
    {
        // 2026-10-02 is Friday of Week 40 in 2026
        var date = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
        var week = IsoWeek.FromDate(date);

        Assert.Equal(2026, week.Year);
        Assert.Equal(40, week.WeekNumber);
        Assert.Equal("2026-W40", week.Value);
    }
}
