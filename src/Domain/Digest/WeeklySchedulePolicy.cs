using System.Globalization;
using LinerNotes.Domain.Enums;

namespace LinerNotes.Domain.Digest;

/// <summary>UTC slots only; the profile time-zone label does not reinterpret DeliveryHourUtc.</summary>
public static class WeeklySchedulePolicy
{
    /// <summary>Returns the first whole-hour slot matching the delivery day and UTC hour at or after <paramref name="nowUtc"/>.</summary>
    /// <exception cref="ArgumentException">The timestamp is not UTC, the day is undefined, or the hour is outside 0–23.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The next slot exceeds the supported date range.</exception>
    public static DateTime NextUtc(DateTime nowUtc, DigestDeliveryDay day, int hourUtc)
    {
        RequireUtc(nowUtc);
        if (!Enum.IsDefined(day) || hourUtc is < 0 or > 23) throw new ArgumentException("invalid_schedule_state");
        var slot = nowUtc.Date.AddDays(((int)day - (int)nowUtc.DayOfWeek + 7) % 7).AddHours(hourUtc);
        return slot < nowUtc ? slot.AddDays(7) : slot;
    }

    /// <summary>Requires a UTC timestamp exactly matching the delivery weekday and hour, with no fractional hour.</summary>
    /// <exception cref="ArgumentException">The timestamp is not UTC, the preferences are invalid, or the slot does not match them.</exception>
    public static void ValidateSlot(DateTime dueUtc, DigestDeliveryDay day, int hourUtc)
    {
        RequireUtc(dueUtc);
        if (!Enum.IsDefined(day) || hourUtc is < 0 or > 23 || (int)dueUtc.DayOfWeek != (int)day ||
            dueUtc.TimeOfDay != TimeSpan.FromHours(hourUtc)) throw new ArgumentException("invalid_schedule_state");
    }

    /// <summary>Returns the ISO week and ISO week-year containing the UTC due date.</summary>
    /// <exception cref="ArgumentException">The timestamp is not UTC.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The ISO week-year is outside 1900–2200.</exception>
    public static IsoWeek Week(DateTime dueUtc)
    {
        RequireUtc(dueUtc);
        return new(ISOWeek.GetYear(dueUtc), ISOWeek.GetWeekOfYear(dueUtc));
    }

    /// <summary>Returns seven days after the stored UTC due slot, even if the result is still overdue.</summary>
    /// <exception cref="ArgumentException">The timestamp is not UTC.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Adding seven days exceeds the supported date range.</exception>
    public static DateTime Advance(DateTime dueUtc)
    {
        RequireUtc(dueUtc);
        return dueUtc.AddDays(7);
    }

    /// <summary>Rejects timestamps whose kind is not UTC with an <see cref="ArgumentException"/>.</summary>
    private static void RequireUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("utc_schedule_required");
    }
}
