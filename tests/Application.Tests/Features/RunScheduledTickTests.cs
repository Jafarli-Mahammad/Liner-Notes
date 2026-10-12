using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Digests.Commands.CaptureLocalDigest;
using LinerNotes.Application.Features.Digests.Commands.GenerateDigest;
using LinerNotes.Application.Features.Digests.Commands.RunScheduledTick;
using MediatR;
using NSubstitute;
using Xunit;

namespace LinerNotes.Application.Tests.Features;

public sealed class RunScheduledTickTests
{
    private static readonly DateTime Due = new(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc);
    private readonly Guid _id = Guid.NewGuid();

    private (RunScheduledTickCommandHandler Handler, Store Store, ISender Sender) Setup()
    {
        var store = new Store(new(_id, "Monday", 8, Due));
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GenerateDigestCommand>(), Arg.Any<CancellationToken>()).Returns(new GenerateDigestResult("generated", null, []));
        sender.Send(Arg.Any<CaptureLocalDigestCommand>(), Arg.Any<CancellationToken>()).Returns(new LocalCaptureResult("local_captured", Guid.NewGuid()));
        return (new(store, sender, new Clock()), store, sender);
    }

    [Fact]
    public async Task VerifiedCapture_AdvancesOnlyTheClaimedWeek()
    {
        var (handler, store, sender) = Setup();
        sender.Send(Arg.Any<GenerateDigestCommand>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var command = call.ArgAt<GenerateDigestCommand>(0);
            Assert.Equal(_id, command.UserId); Assert.Equal("2026-W42", command.Week.Value);
            return Task.FromResult(new GenerateDigestResult("existing", null, []));
        });
        var result = await handler.Handle(new([_id], 1), default);
        Assert.Equal("completed", result.Status); Assert.Equal("advanced", Assert.Single(result.Jobs).Status);
        Assert.Equal(Due.AddDays(7), store.User.NextDigestAt); Assert.True(store.Released);
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("capture")]
    [InlineData("advance")]
    [InlineData("lease")]
    public async Task FailureOrConflict_DoesNotAdvance(string stage)
    {
        var (handler, store, sender) = Setup();
        if (stage == "generation") sender.Send(Arg.Any<GenerateDigestCommand>(), Arg.Any<CancellationToken>()).Returns(new GenerateDigestResult("stopped", null, [], "fixture_stop"));
        if (stage == "capture") sender.Send(Arg.Any<CaptureLocalDigestCommand>(), Arg.Any<CancellationToken>()).Returns(new LocalCaptureResult("stopped", Reason: "email_unsubscribed"));
        if (stage == "advance") store.RejectAdvance = true;
        if (stage == "lease") sender.Send(Arg.Any<CaptureLocalDigestCommand>(), Arg.Any<CancellationToken>()).Returns(_ =>
        { store.LostLease = true; return Task.FromResult(new LocalCaptureResult("already_captured")); });
        var result = await handler.Handle(new([_id], 1), default);
        Assert.Equal("stopped", result.Status); Assert.Equal(Due, store.User.NextDigestAt); Assert.True(store.Released);
    }

    [Fact]
    public async Task CancellationAfterCapture_LeavesTheSlotDueAndReleasesLease()
    {
        var (handler, store, sender) = Setup(); using var cancelled = new CancellationTokenSource();
        sender.Send(Arg.Any<CaptureLocalDigestCommand>(), Arg.Any<CancellationToken>()).Returns(_ =>
        { cancelled.Cancel(); return Task.FromResult(new LocalCaptureResult("local_captured")); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(new([_id], 1), cancelled.Token));
        Assert.Equal(Due, store.User.NextDigestAt); Assert.True(store.Released);
    }

    [Fact]
    public async Task NullSlot_InitializesInTheFutureWithoutCapturing()
    {
        var (handler, store, sender) = Setup(); store.User = store.User with { NextDigestAt = null, DeliveryDay = "Friday", DeliveryHourUtc = 17 };
        sender.Send(Arg.Any<GenerateDigestCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GenerateDigestResult>(new InvalidOperationException("must_not_generate")));
        var result = await handler.Handle(new([_id], 1), default);
        Assert.Equal("completed", result.Status); Assert.Equal("initialized", Assert.Single(result.Jobs).Status);
        Assert.Equal(new DateTime(2026, 10, 16, 17, 0, 0, DateTimeKind.Utc), store.User.NextDigestAt);
    }

    [Fact]
    public async Task BusyOrInactiveAccount_DoesNotDoWork()
    {
        var (handler, store, _) = Setup(); store.Busy = true;
        Assert.Equal("scheduler_busy", (await handler.Handle(new([_id], 1), default)).Status);
        Assert.Equal(Due, store.User.NextDigestAt);
        store.Busy = false; store.Inactive = true;
        Assert.Equal("inactive", Assert.Single((await handler.Handle(new([_id], 1), default)).Jobs).Status);
        Assert.Equal(Due, store.User.NextDigestAt);
    }

    [Theory]
    [InlineData("Funday", 8)]
    [InlineData("1", 8)]
    [InlineData("Monday", 24)]
    [InlineData("Friday", 8)]
    public async Task InvalidRawSchedule_IsReportedWithoutRepair(string day, int hour)
    {
        var (handler, store, _) = Setup(); store.User = store.User with { DeliveryDay = day, DeliveryHourUtc = hour };
        var result = await handler.Handle(new([_id], 1), default);
        Assert.Equal("invalid_schedule_state", Assert.Single(result.Jobs).Reason); Assert.Equal(Due, store.User.NextDigestAt);
    }

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Due.AddHours(1)); }
    private sealed class Store(ScheduledUser user) : IWeeklyScheduleStore, IScheduledTickLease
    {
        public ScheduledUser User = user;
        public bool Busy, Inactive, RejectAdvance, LostLease, Released;
        public Task<IScheduledTickLease?> TryAcquireTickAsync(CancellationToken ct) => Task.FromResult<IScheduledTickLease?>(Busy ? null : this);
        public Task EnsureHeldAsync(CancellationToken ct) => LostLease ? Task.FromException(new GenerationStoppedException("schedule_lease_lost")) : Task.CompletedTask;
        public Task<IReadOnlyList<ScheduledUser>> GetCandidatesAsync(IReadOnlyList<Guid> users, int limit, DateTime now, CancellationToken ct) => Task.FromResult<IReadOnlyList<ScheduledUser>>([User]);
        public Task<ScheduledUser?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<ScheduledUser?>(Inactive ? null : User);
        public Task<bool> InitializeAsync(ScheduledUser observed, DateTime next, DateTime now, CancellationToken ct)
        { User = User with { NextDigestAt = next }; return Task.FromResult(true); }
        public Task<bool> AdvanceAsync(ScheduledUser observed, DateTime next, DateTime now, CancellationToken ct)
        { if (!RejectAdvance) User = User with { NextDigestAt = next }; return Task.FromResult(!RejectAdvance); }
        public ValueTask DisposeAsync() { Released = true; return ValueTask.CompletedTask; }
    }
}
