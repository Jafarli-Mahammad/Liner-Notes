#:property PublishAot=false
#:project ../src/Worker/Worker.csproj

using System.Globalization;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.DataAccess;
using LinerNotes.DataAccess.Persistence.Repositories;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

// Opt-in local legacy repair. Never invoked by scheduler ticks.
try
{
    var builder = Host.CreateApplicationBuilder(args);
    if (!builder.Environment.IsDevelopment() ||
        !Guid.TryParse(builder.Configuration["user"], out var user) || user == Guid.Empty ||
        !DateTime.TryParse(builder.Configuration["expected"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expected) ||
        expected.Kind != DateTimeKind.Utc ||
        !DateTime.TryParse(builder.Configuration["replacement"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var replacement) ||
        replacement.Kind != DateTimeKind.Utc)
        throw new ArgumentException();
    var connection = new NpgsqlConnectionStringBuilder(builder.Configuration["ConnectionStrings:DefaultConnection"]);
    if (connection.Host is not ("127.0.0.1" or "::1" or "localhost")) throw new ArgumentException();
    builder.Services.AddDataAccess(builder.Configuration);
    using var host = builder.Build();
    await using var scope = host.Services.CreateAsyncScope();
    var services = scope.ServiceProvider;
    var store = (WeeklyScheduleStore)services.GetRequiredService<IWeeklyScheduleStore>();
    await using var scheduler = await store.TryAcquireTickAsync(default);
    if (scheduler is null) throw new InvalidOperationException();
    await using var generation = await services.GetRequiredService<IDigestGenerationStore>().AcquireBatchAsync(default);
    await using var ownership = await services.GetRequiredService<IAccountEmailLease>().AcquireAsync(user, default);
    var observed = await store.GetAsync(user, default) ?? throw new InvalidOperationException();
    if (observed.NextDigestAt != expected || !Enum.TryParse<DigestDeliveryDay>(observed.DeliveryDay, out var day))
        throw new InvalidOperationException();
    WeeklySchedulePolicy.ValidateSlot(replacement, day, observed.DeliveryHourUtc);
    if (builder.Configuration["apply"] != "true")
    {
        Console.WriteLine(JsonSerializer.Serialize(new { Status = "preview", ExpectedUtc = expected, ReplacementUtc = replacement,
            Note = "Apply additionally checks the legacy registration pattern and absence of digest work." }));
        return 0;
    }
    if (!await store.RepairRegistrationSlotAsync(observed, replacement, DateTime.UtcNow, default))
        throw new InvalidOperationException();
    Console.WriteLine("{\"Status\":\"registration_slot_repaired\"}");
    return 0;
}
catch (Exception)
{
    Console.WriteLine("{\"Status\":\"stopped\",\"Reason\":\"registration_slot_repair_rejected\"}");
    return 2;
}
