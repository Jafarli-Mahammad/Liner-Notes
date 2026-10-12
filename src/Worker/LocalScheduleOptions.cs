using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace LinerNotes.Worker;

public sealed record LocalScheduleOptions(IReadOnlyList<Guid> Users, int MaxUsersPerTick, int MaxTicks, TimeSpan Interval)
{
    public static LocalScheduleOptions Parse(IConfiguration configuration, string[]? arguments = null)
    {
        // Host environment configuration includes the OS USER variable. Only command-line
        // legacy selectors are forbidden when parsing a real process invocation.
        var selectors = arguments is null ? configuration : new ConfigurationBuilder().AddCommandLine(arguments).Build();
        if (configuration["action"] != "schedule" || configuration["origin"] != "Synthetic" ||
            selectors["user"] is not null || selectors["week"] is not null)
            throw new ArgumentException("explicit_synthetic_schedule_required");
        var users = configuration.GetSection("Schedule:Users").GetChildren().Select(child =>
            Guid.TryParse(child.Value, out var id) && id != Guid.Empty ? id : throw new ArgumentException("schedule_users_required")).ToArray();
        if (users.Length is < 1 or > 100 || users.Distinct().Count() != users.Length)
            throw new ArgumentException("bounded_distinct_schedule_users_required");
        int Limit(string key, int fallback, int maximum) => configuration[key] is not { } raw ? fallback :
            int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1 && value <= maximum
                ? value : throw new ArgumentException("invalid_schedule_limits");
        return new(Array.AsReadOnly(users), Limit("Schedule:MaxUsersPerTick", 10, 100),
            Limit("Schedule:MaxTicks", 1, 1000), TimeSpan.FromSeconds(Limit("Schedule:IntervalSeconds", 60, 3600)));
    }
}
