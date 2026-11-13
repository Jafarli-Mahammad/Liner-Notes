using LinerNotes.Application.Common.Diagnostics;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using LinerNotes.Domain.Discovery;

namespace LinerNotes.Presentation.Production;

public sealed class OperationalHealth(IServiceScopeFactory scopes, OperationalStatus status,
    CoordinatedStorageOptions storage, IStorageFreeSpaceProbe probe, TimeProvider clock) : BackgroundService
{
    public static bool Ready(IReadOnlyDictionary<OperationalComponent, OperationalObservation> snapshot) =>
        new[] { OperationalComponent.Storage, OperationalComponent.Database, OperationalComponent.Disk }
            .All(component => snapshot.TryGetValue(component, out var observation) && observation.Healthy);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30), clock);
        do
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<DataContext>();
                var connected = await database.Database.CanConnectAsync(deadline.Token);
                status.Report(OperationalComponent.Database, connected, "database_unavailable");
                if (connected)
                {
                    var now = clock.GetUtcNow();
                    var queue = await database.DiscoveryJobs.Where(j => j.State == DiscoveryJobState.Pending ||
                        j.State == DiscoveryJobState.Running || j.State == DiscoveryJobState.Retryable).GroupBy(_ => 1)
                        .Select(g => new OperationalQueue(g.Count(j => j.State != DiscoveryJobState.Running),
                            g.Count(j => j.State == DiscoveryJobState.Running), g.Min(j => (DateTimeOffset?)j.CreatedAtUtc),
                            g.Where(j => j.State == DiscoveryJobState.Running && j.LeaseExpiresAtUtc < now)
                                .Min(j => j.LeaseExpiresAtUtc))).SingleOrDefaultAsync(deadline.Token);
                    status.ReportQueue(queue ?? new(0, 0, null, null));
                    var emailOptions = scope.ServiceProvider.GetRequiredService<LinerNotes.Application.Common.Interfaces.EmailDeliveryOptions>();
                    if (emailOptions.Enabled)
                    {
                        var mail = await scope.ServiceProvider.GetRequiredService<LinerNotes.Application.Common.Interfaces.IEmailDispatchStore>()
                            .SummaryAsync(now, emailOptions.DailyLimit, deadline.Token);
                        status.ReportMail(mail);
                        bool healthy = mail.Ambiguous == 0 && mail.RecentFailures == 0 &&
                            !(mail.Pending > 0 && mail.RemainingDailyBudget == 0) &&
                            !(mail.OldestPendingAt < now.AddMinutes(-15));
                        status.Report(OperationalComponent.Email, healthy, "email_dispatch_attention_required");
                    }

                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch { status.Report(OperationalComponent.Database, false, "database_unavailable"); }
            try { storage.RequireHeadroom(0, probe); status.Report(OperationalComponent.Disk, true); }
            catch { status.Report(OperationalComponent.Disk, false, "disk_headroom_unavailable"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
