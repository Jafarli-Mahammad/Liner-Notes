#:property PublishAot=false
#:project ../src/Worker/Worker.csproj

using System.Globalization;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.DataAccess;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

// Explicit disposable-account fixture setup; never enrolls a production account.
try
{
    var builder = Host.CreateApplicationBuilder(args);
    if (!builder.Environment.IsDevelopment() || !Guid.TryParse(builder.Configuration["user"], out var user) || user == Guid.Empty ||
        !DateTime.TryParse(builder.Configuration["due"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var due) ||
        due.Kind != DateTimeKind.Utc)
        throw new ArgumentException();
    var connection = new NpgsqlConnectionStringBuilder(builder.Configuration["ConnectionStrings:DefaultConnection"]);
    if (connection.Host is not ("127.0.0.1" or "::1" or "localhost") || connection.Database != "liner_notes_manual_qa")
        throw new ArgumentException();
    var day = (DigestDeliveryDay)due.DayOfWeek;
    WeeklySchedulePolicy.ValidateSlot(due, day, due.Hour);
    builder.Services.AddDataAccess(builder.Configuration);
    using var host = builder.Build();
    await using var scope = host.Services.CreateAsyncScope();
    var services = scope.ServiceProvider;
    await using var scheduler = await services.GetRequiredService<IWeeklyScheduleStore>().TryAcquireTickAsync(default);
    if (scheduler is null) throw new InvalidOperationException();
    await using var ownership = await services.GetRequiredService<IAccountEmailLease>().AcquireAsync(user, default);
    var db = services.GetRequiredService<DataContext>();
    var updated = await db.Users.Where(u => u.Id == user && !u.IsDeleted && u.EmailUnsubscribedAtUtc == null &&
        db.ApplicationUsers.Any(i => i.Id == user && !i.IsDeleted)).ExecuteUpdateAsync(setters => setters
            .SetProperty(u => u.DeliveryDay, day).SetProperty(u => u.DeliveryHourUtc, due.Hour)
            .SetProperty(u => u.NextDigestAt, due).SetProperty(u => u.LastModifiedAt, DateTime.UtcNow)
            .SetProperty(u => u.LastModifiedBy, (Guid?)null));
    if (updated != 1) throw new InvalidOperationException();
    Console.WriteLine(JsonSerializer.Serialize(new { Status = "schedule_fixture_set", DueUtc = due, Week = WeeklySchedulePolicy.Week(due).Value }));
    return 0;
}
catch (Exception)
{
    Console.WriteLine("{\"Status\":\"stopped\",\"Reason\":\"disposable_schedule_fixture_required\"}");
    return 2;
}
