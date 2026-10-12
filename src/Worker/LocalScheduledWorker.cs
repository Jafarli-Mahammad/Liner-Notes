using LinerNotes.Application.Features.Digests.Commands.RunScheduledTick;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LinerNotes.Worker;

public sealed record ScheduleRunResult(string Status, int TicksCompleted, IReadOnlyList<ScheduleTickResult> Ticks, string? Reason = null);

public sealed class LocalScheduleRunState
{
    public ScheduleRunResult Result { get; internal set; } = new("not_started", 0, []);
}

public sealed class LocalScheduledWorker(IServiceScopeFactory scopes, LocalScheduleOptions options,
    TimeProvider clock, IHostApplicationLifetime lifetime, LocalScheduleRunState state) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield(); // Let host startup complete even when an empty tick completes synchronously.
        var ticks = new List<ScheduleTickResult>();
        try
        {
            for (var index = 0; index < options.MaxTicks; index++)
            {
                stoppingToken.ThrowIfCancellationRequested();
                ScheduleTickResult result;
                await using (var scope = scopes.CreateAsyncScope())
                    result = await scope.ServiceProvider.GetRequiredService<ISender>()
                        .Send(new RunScheduledTickCommand(options.Users, options.MaxUsersPerTick), stoppingToken).ConfigureAwait(false);
                ticks.Add(result);
                if (result.Status == "scheduler_busy") break;
                if (index + 1 < options.MaxTicks)
                    await Task.Delay(options.Interval, clock, stoppingToken).ConfigureAwait(false);
            }
            state.Result = new(ticks.Any(tick => tick.Status is "stopped" or "scheduler_busy") ? "stopped" : "completed",
                ticks.Count, ticks.ToArray());
        }
        catch (OperationCanceledException) { state.Result = new("cancelled", ticks.Count, ticks.ToArray()); }
        catch (Exception) { state.Result = new("stopped", ticks.Count, ticks.ToArray(), "scheduler_unavailable"); }
        finally { lifetime.StopApplication(); }
    }
}
