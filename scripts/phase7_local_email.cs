#:property PublishAot=false
#:project ../src/Worker/Worker.csproj

using System.Text.Json;
using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Export.Queries.GetUserDataExport;
using LinerNotes.DataAccess;
using LinerNotes.Domain.Digest;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.Email;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

// Explicit local operator inspection/conversion; plaintext is never saved by this helper.
try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Logging.ClearProviders();
    string? action = builder.Configuration["action"];
    if (!builder.Environment.IsDevelopment() || action is not ("inspect" or "convert" or "unsubscribe-link") ||
        !Guid.TryParse(builder.Configuration["user"], out var userId) || userId == Guid.Empty)
        throw new ArgumentException();
    var week = IsoWeek.Parse(builder.Configuration["week"] ?? "");
    if (week.Value != builder.Configuration["week"] || week.WeekNumber > System.Globalization.ISOWeek.GetWeeksInYear(week.Year))
        throw new ArgumentException();
    var connection = new NpgsqlConnectionStringBuilder(builder.Configuration["ConnectionStrings:DefaultConnection"]);
    if (connection.Host is not ("127.0.0.1" or "::1" or "localhost")) throw new ArgumentException();
    builder.Services.AddApplication().AddDataAccess(builder.Configuration).AddInfrastructure(builder.Configuration);
    using var host = builder.Build();
    await using var scope = host.Services.CreateAsyncScope();
    var services = scope.ServiceProvider;
    await using var lease = await services.GetRequiredService<IAccountEmailLease>().AcquireAsync(userId, CancellationToken.None);
    var store = services.GetRequiredService<IDigestGenerationStore>();
    var digest = await store.GetExistingAsync(userId, week);
    if (digest is null) throw new ArgumentException();
    if (action == "convert")
    {
        // Provision shared keys before computing the database revision. Conversion requires reconciled headroom.
        _ = services.GetRequiredService<IUnsubscribeTokens>().Create(userId);
        string status = await services.GetRequiredService<LocalEmailLegacyConversion>()
            .ConvertAsync(userId, digest.Id, await store.ReadStorageAsync(), CancellationToken.None);
        Console.WriteLine(JsonSerializer.Serialize(new { Status = status }));
    }
    else
    {
        // The export handler verifies the subscriber and uses the same sanitized archive as authenticated HTTP export.
        var export = await services.GetRequiredService<ISender>().Send(new GetUserDataExportQuery(userId));
        var copy = export.LocalEmail?.Copies.SingleOrDefault(c => c.DigestId == digest.Id);
        if (copy is null) throw new ArgumentException();
        if (action == "inspect") Console.WriteLine(copy.PlainText);
        else
        {
            var stored = await services.GetRequiredService<LocalEmailArchive>().ReadForOperatorAsync(userId, digest.Id, CancellationToken.None);
            var origin = services.GetRequiredService<LocalEmailOptions>().Origin();
            Console.WriteLine(new Uri(origin, "api/digests/unsubscribe?token=" + Uri.EscapeDataString(stored.Receipt.Token)).AbsoluteUri);
        }
    }
    return 0;
}
catch (GenerationStoppedException ex)
{
    Console.WriteLine(JsonSerializer.Serialize(new { Status = "stopped", Reason = ex.Message }));
    return 2;
}
catch (Exception)
{
    Console.WriteLine("{\"Status\":\"stopped\",\"Reason\":\"local_email_inspection_or_conversion_unavailable\"}");
    return 2;
}
