using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Domain.Discovery;
using NSubstitute;
using Xunit;

namespace LinerNotes.Application.Tests.Features.Discovery;

public sealed class GuestActivityTests
{
    [Fact]
    public async Task ReadsRenewActivityAtMostHourly()
    {
        var clock = new Clock();
        var profile = ListenerProfile.Guest(clock.Now.AddHours(-2), GuestLifecycle.Inactivity);
        var store = Substitute.For<IGuestProfileStore>();
        store.FindGuestAsync("credential", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(profile);
        store.TouchAsync(profile.Id, clock.Now, GuestLifecycle.Inactivity, GuestLifecycle.ActivityInterval, Arg.Any<CancellationToken>())
            .Returns(_ => { profile.Touch(clock.Now, GuestLifecycle.Inactivity, GuestLifecycle.ActivityInterval); return Task.CompletedTask; });
        var writes = Substitute.For<IDiscoveryWriteCoordinator>();
        writes.ExecuteAsync(16_384, Arg.Any<Func<CancellationToken, Task<bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<bool>>>()(call.Arg<CancellationToken>()));
        var lifecycle = new GuestLifecycle(store, writes, clock);

        Assert.Same(profile, await lifecycle.ResolveGuestAsync("credential", true, default));
        Assert.Equal(clock.Now.AddDays(7), profile.ExpiresAtUtc);
        Assert.Same(profile, await lifecycle.ResolveGuestAsync("credential", true, default));

        await store.Received(1).TouchAsync(profile.Id, clock.Now, GuestLifecycle.Inactivity, GuestLifecycle.ActivityInterval, Arg.Any<CancellationToken>());
        await store.Received(3).FindGuestAsync("credential", clock.Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoppedActivityWritesKeepExistingDataAndActualExpiryReadable()
    {
        var clock = new Clock();
        var profile = ListenerProfile.Guest(clock.Now.AddHours(-2), GuestLifecycle.Inactivity);
        var expiry = profile.ExpiresAtUtc;
        var store = Substitute.For<IGuestProfileStore>();
        store.FindGuestAsync("credential", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(profile);
        var writes = Substitute.For<IDiscoveryWriteCoordinator>();
        writes.ExecuteAsync(16_384, Arg.Any<Func<CancellationToken, Task<bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new GenerationStoppedException("storage_headroom")));

        var actual = await new GuestLifecycle(store, writes, clock).ResolveGuestAsync("credential", true, default);

        Assert.Same(profile, actual);
        Assert.Equal(expiry, actual!.ExpiresAtUtc);
        await store.DidNotReceive().TouchAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
