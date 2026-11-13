using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Features.Digests.Commands.RunScheduledTick;
using LinerNotes.Application.Services;
using LinerNotes.Infrastructure.Email;
using MediatR;
using Microsoft.Extensions.Configuration;
namespace LinerNotes.Worker;

public sealed record ProductionMailResult(string Status, ScheduleTickResult Schedule, EmailTickResult Delivery);
public sealed class ProductionMailRunner(ISender sender, EmailDispatcher dispatcher, IConfiguration configuration)
{
    public async Task<ProductionMailResult> RunAsync(CancellationToken ct)
    {
        MailActivation.Validate(configuration, production: true);
        if (!configuration.GetValue<bool>("EmailDelivery:Enabled") || configuration["Generation:Origin"] != "Live" ||
            configuration["Generation:Provider"] != "listenbrainz" || configuration["ListenBrainz:Mode"] != "LiveOnly" ||
            !configuration.GetValue<bool>("ListenBrainz:LiveUseApproved") || !configuration.GetValue<bool>("Generation:LiveEnabled") ||
            !configuration.GetValue<bool>("DiscoveryRuntime:EnforceStorageAdmission") || configuration.GetValue<bool>("LocalEmail:Enabled"))
            throw new InvalidOperationException("production_mail_configuration_required");
        int users = configuration.GetValue<int?>("Schedule:MaxUsersPerTick") ?? 5;
        int attempts = configuration.GetValue<int?>("Schedule:MaxDispatchesPerTick") ?? 10;
        if (users is < 1 or > 100 || attempts is < 1 or > 100) throw new ArgumentException("invalid_schedule_limits");
        // Account recovery must not wait behind slow or unavailable music acquisition.
        var first = await dispatcher.RunAsync(attempts, ct);
        var schedule = await sender.Send(new RunScheduledTickCommand([], users, Production: true), ct);
        int remaining = Math.Max(0, attempts - first.Accepted - first.Deferred - first.Stopped);
        var after = remaining > 0 ? await dispatcher.RunAsync(remaining, ct) : new EmailTickResult(0, 0, 0);
        var delivery = new EmailTickResult(first.Accepted + after.Accepted, first.Deferred + after.Deferred,
            first.Stopped + after.Stopped, first.Busy || after.Busy);
        return new(schedule.Status == "stopped" || delivery.Stopped > 0 ? "stopped" : "completed", schedule, delivery);
    }
}
