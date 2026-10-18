using LinerNotes.Application.Features.Catalog;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Domain.Discovery;

namespace LinerNotes.Presentation.Background;

public sealed class DiscoveryJobService(IServiceScopeFactory scopes, DiscoveryRuntimeOptions options,
    TimeProvider clock, ILogger<DiscoveryJobService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        options.Validate();
        return options.RunJobs ? Task.WhenAll(LaneAsync(false, stoppingToken), LaneAsync(true, stoppingToken)) : Task.CompletedTask;
    }

    private async Task LaneAsync(bool acquisition, CancellationToken ct)
    {
        var lane = acquisition ? DiscoveryJobLane.Acquisition : DiscoveryJobLane.Prepared;
        bool preferLookup = true;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var jobs = scope.ServiceProvider.GetRequiredService<IDiscoveryJobStore>();
                DiscoveryJob? job = null;
                var order = !acquisition ? new[] { DiscoveryJobKind.Generation } : preferLookup
                    ? [DiscoveryJobKind.Lookup, DiscoveryJobKind.Generation] : [DiscoveryJobKind.Generation, DiscoveryJobKind.Lookup];
                foreach (var kind in order)
                {
                    if (!await jobs.HasReadyAsync(kind, lane, clock.GetUtcNow(), options.MaximumAttempts, ct)) continue;
                    job = await scope.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(16_384,
                        token => jobs.LeaseNextAsync(kind, clock.GetUtcNow(), TimeSpan.FromSeconds(options.LeaseSeconds), options.MaximumAttempts, token, lane), ct);
                    if (job is not null) break;
                }
                if (job is null) { await Task.Delay(TimeSpan.FromSeconds(1), clock, ct); continue; }
                if (acquisition) preferLookup = !preferLookup;
                await ProcessAsync(job, acquisition, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception)
            {
                logger.LogWarning("Discovery runner is waiting for writable storage or valid configuration.");
                await Task.Delay(TimeSpan.FromSeconds(5), clock, ct);
            }
        }
    }

    private async Task ProcessAsync(DiscoveryJob job, bool acquisition, CancellationToken stoppingToken)
    {
        using var work = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        work.CancelAfter(TimeSpan.FromSeconds(options.MaximumWorkSeconds));
        using var heartbeatStop = CancellationTokenSource.CreateLinkedTokenSource(work.Token);
        var heartbeat = HeartbeatAsync(job, work, heartbeatStop.Token);
        string? error = null;
        bool retry = false, defer = false;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            if (job.Kind == DiscoveryJobKind.Lookup)
                await scope.ServiceProvider.GetRequiredService<RunCatalogLookup>().ExecuteAsync(job, work.Token);
            else await scope.ServiceProvider.GetRequiredService<RunDiscoveryJob>().ExecuteAsync(job, acquisition, work.Token);
        }
        catch (DiscoveryPreparationRequiredException) { defer = true; }
        catch (PublicPreparationBusyException) { defer = true; }
        catch (DiscoverySourceException ex) { error = ex.Message; retry = ex.Retryable; }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { error = "preparation_timeout"; retry = true; }
        catch (GenerationStoppedException) { error = "generation_unavailable"; }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            // Code locations help operators diagnose faults without logging messages, payloads or provider bodies.
            var locations = (new System.Diagnostics.StackTrace(ex, false).GetFrames() ?? []).Take(4).Select(frame =>
                $"{frame.GetMethod()?.DeclaringType?.FullName}.{frame.GetMethod()?.Name}");
            logger.LogWarning("Discovery job failed with {FailureType} in {CodeLocations}.",
                ex.GetType().FullName, string.Join(" > ", locations));
            error = "generation_failed";
        }
        finally
        {
            await heartbeatStop.CancelAsync();
            try { await heartbeat; } catch (OperationCanceledException) { }
        }
        if (stoppingToken.IsCancellationRequested) return;
        if (!defer && error is null) return;
        await using var status = scopes.CreateAsyncScope();
        var jobs = status.ServiceProvider.GetRequiredService<IDiscoveryJobStore>();
        await status.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(16_384, async token => {
            if (defer) await jobs.DeferAsync(job.Id, job.LeaseToken!.Value, clock.GetUtcNow(), TimeSpan.FromSeconds(1), "waiting_for_music", token);
            else if (error is not null) await jobs.FailAsync(job.Id, job.LeaseToken!.Value, clock.GetUtcNow(), error, retry,
                TimeSpan.FromSeconds(Math.Min(60, job.Attempts * 5)), options.MaximumAttempts, token);
            return true;
        }, stoppingToken);
    }

    private async Task HeartbeatAsync(DiscoveryJob job, CancellationTokenSource work, CancellationToken ct)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, options.LeaseSeconds / 3)), clock, ct);
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(16_384, async token => {
                    await scope.ServiceProvider.GetRequiredService<IDiscoveryJobStore>().RenewAsync(job.Id, job.LeaseToken!.Value,
                        clock.GetUtcNow(), TimeSpan.FromSeconds(options.LeaseSeconds), "preparing", token);
                    return true;
                }, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception) { await work.CancelAsync(); }
    }
}
