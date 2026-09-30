using System.Globalization;

namespace LinerNotes.Domain.Digest;

/// <summary>
/// Value object representing an ISO-8601 week (e.g. 2026-W40).
/// Guarantees consistent weekly batch boundaries and enforces weekly delivery idempotency.
/// </summary>
public sealed class IsoWeek : IEquatable<IsoWeek>, IComparable<IsoWeek>
{
    public int Year { get; }
    public int WeekNumber { get; }

    public string Value => $"{Year:D4}-W{WeekNumber:D2}";

    public IsoWeek(int year, int weekNumber)
    {
        if (year < 1900 || year > 2200)
            throw new ArgumentOutOfRangeException(nameof(year), "Year must be between 1900 and 2200.");
        if (weekNumber < 1 || weekNumber > 53)
            throw new ArgumentOutOfRangeException(nameof(weekNumber), "Week number must be between 1 and 53.");

        Year = year;
        WeekNumber = weekNumber;
    }

    public static IsoWeek From(int year, int weekNumber) => new(year, weekNumber);

    public static IsoWeek FromDate(DateTime utcDate)
    {
        var calendar = CultureInfo.InvariantCulture.Calendar;
        var week = calendar.GetWeekOfYear(utcDate, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        var year = utcDate.Year;

        // If week 52/53 falls into early January
        if (week >= 52 && utcDate.Month == 1)
        {
            year--;
        }
        // If week 1 falls into late December
        else if (week == 1 && utcDate.Month == 12)
        {
            year++;
        }

        return new IsoWeek(year, week);
    }

    public static IsoWeek Parse(string isoWeekString)
    {
        if (string.IsNullOrWhiteSpace(isoWeekString))
            throw new ArgumentException("ISO week string cannot be empty.", nameof(isoWeekString));

        var parts = isoWeekString.Trim().Split("-W", StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out var year) ||
            !int.TryParse(parts[1], out var week))
        {
            throw new FormatException($"Invalid ISO week format: '{isoWeekString}'. Expected format 'YYYY-Www' (e.g. 2026-W40).");
        }

        return new IsoWeek(year, week);
    }

    public bool Equals(IsoWeek? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Year == other.Year && WeekNumber == other.WeekNumber;
    }

    public override bool Equals(object? obj) => Equals(obj as IsoWeek);

    public override int GetHashCode() => HashCode.Combine(Year, WeekNumber);

    public int CompareTo(IsoWeek? other)
    {
        if (other is null) return 1;
        var yearComp = Year.CompareTo(other.Year);
        return yearComp != 0 ? yearComp : WeekNumber.CompareTo(other.WeekNumber);
    }

    public override string ToString() => Value;

    public static bool operator ==(IsoWeek? left, IsoWeek? right) => Equals(left, right);
    public static bool operator !=(IsoWeek? left, IsoWeek? right) => !Equals(left, right);
}
