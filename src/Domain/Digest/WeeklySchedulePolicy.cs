using System.Globalization;
using LinerNotes.Domain.Enums;

namespace LinerNotes.Domain.Digest;

/// <summary>UTC slots only; the profile time-zone label does not reinterpret DeliveryHourUtc.</summary>
public static class WeeklySchedulePolicy
{
    public static DateTime NextUtc(DateTime nowUtc, DigestDeliveryDay day, int hourUtc)
    {
        RequireUtc(nowUtc);
        if (!Enum.IsDefined(day) || hourUtc is < 0 or > 23) throw new ArgumentException("invalid_schedule_state");
        var slot = nowUtc.Date.AddDays(((int)day - (int)nowUtc.DayOfWeek + 7) % 7).AddHours(hourUtc);
        return slot < nowUtc ? slot.AddDays(7) : slot;
    }

    public static void ValidateSlot(DateTime dueUtc, DigestDeliveryDay day, int hourUtc)
    {
        RequireUtc(dueUtc);
        if (!Enum.IsDefined(day) || hourUtc is < 0 or > 23 || (int)dueUtc.DayOfWeek != (int)day ||
            dueUtc.TimeOfDay != TimeSpan.FromHours(hourUtc)) throw new ArgumentException("invalid_schedule_state");
    }

    public static IsoWeek Week(DateTime dueUtc)
    {
        RequireUtc(dueUtc);
        return new(ISOWeek.GetYear(dueUtc), ISOWeek.GetWeekOfYear(dueUtc));
    }

    public static DateTime Advance(DateTime dueUtc)
    {
        RequireUtc(dueUtc);
        return dueUtc.AddDays(7);
    }

    private static void RequireUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("utc_schedule_required");
    }
}
