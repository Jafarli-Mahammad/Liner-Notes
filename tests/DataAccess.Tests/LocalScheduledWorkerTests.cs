using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed class LocalScheduledWorkerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BusyOrCancellationDuringDelay_EndsAndDisposesTickScope(bool busy)
    {
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifetime = new Lifetime();
        var registrations = new ServiceCollection().AddLogging().AddApplication();
        registrations.AddSingleton<TimeProvider>(TimeProvider.System);
        registrations.AddScoped<IWeeklyScheduleStore>(_ => new Store(busy, disposed));
        await using var services = registrations.BuildServiceProvider();
        var state = new LocalScheduleRunState();
        using var worker = new LocalScheduledWorker(services.GetRequiredService<IServiceScopeFactory>(),
            new([Guid.NewGuid()], 1, 1000, TimeSpan.FromSeconds(60)), TimeProvider.System, lifetime, state);
        await worker.StartAsync(default);
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (!busy) await worker.StopAsync(default);
        await lifetime.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(busy ? "stopped" : "cancelled", state.Result.Status);
        Assert.Equal(1, state.Result.TicksCompleted); Assert.Single(state.Result.Ticks);
        Assert.Equal(busy ? "scheduler_busy" : "completed", state.Result.Ticks[0].Status);
    }

    private sealed class Lifetime : IHostApplicationLifetime
    {
        public TaskCompletionSource Stopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken ApplicationStarted => default;
        public CancellationToken ApplicationStopping => default;
        public CancellationToken ApplicationStopped => default;
        public void StopApplication() => Stopped.TrySetResult();
    }
    private sealed class Store(bool busy, TaskCompletionSource disposed) : IWeeklyScheduleStore, IDisposable
    {
        public Task<IScheduledTickLease?> TryAcquireTickAsync(CancellationToken ct) => Task.FromResult<IScheduledTickLease?>(busy ? null : new Lease());
        public Task<IReadOnlyList<ScheduledUser>> GetCandidatesAsync(IReadOnlyList<Guid> ids, int limit, DateTime now, CancellationToken ct) => Task.FromResult<IReadOnlyList<ScheduledUser>>([]);
        public Task<ScheduledUser?> GetAsync(Guid id, CancellationToken ct) => throw new InvalidOperationException("no_candidate");
        public Task<bool> InitializeAsync(ScheduledUser user, DateTime next, DateTime now, CancellationToken ct) => throw new InvalidOperationException("no_candidate");
        public Task<bool> AdvanceAsync(ScheduledUser user, DateTime next, DateTime now, CancellationToken ct) => throw new InvalidOperationException("no_candidate");
        public void Dispose() => disposed.TrySetResult();
    }
    private sealed class Lease : IScheduledTickLease
    {
        public Task EnsureHeldAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
