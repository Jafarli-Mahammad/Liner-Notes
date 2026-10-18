using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Presentation.Discovery;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public sealed class ListenerContextTests
{
    [Fact]
    public async Task FailedRenewalCookieKeepsTheActualStoredExpiry()
    {
        var clock = new FixedClock();
        var credential = new string('a', 64);
        var profile = LinerNotes.Domain.Discovery.ListenerProfile.Guest(clock.GetUtcNow().AddHours(-2), GuestLifecycle.Inactivity);
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = "liner.guest=" + credential;
        var store = Substitute.For<IGuestProfileStore>();
        store.FindGuestAsync(ListenerContext.HashCredential(credential)!, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(profile);
        var writes = Substitute.For<IDiscoveryWriteCoordinator>();
        writes.ExecuteAsync(16_384, Arg.Any<Func<CancellationToken, Task<bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new LinerNotes.Application.Common.Interfaces.Recommendation.GenerationStoppedException("storage_headroom")));
        var listener = new ListenerContext(new HttpContextAccessor { HttpContext = http }, store,
            new GuestLifecycle(store, writes, clock), Substitute.For<IAntiforgery>(),
            Microsoft.Extensions.Options.Options.Create(new GuestDiscoveryOptions { Enabled = true }), Substitute.For<IHostEnvironment>(), clock);

        Assert.Same(profile, await listener.ResolveAsync(true, default));
        Assert.Contains("max-age=597600", http.Response.Headers.SetCookie.ToString());
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("profile-id-is-not-a-credential")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!")]
    public void InvalidCredentialsAreRejectedBeforeDatabaseLookup(string? value) =>
        Assert.Null(ListenerContext.HashCredential(value));

    [Fact]
    public async Task AuthenticatedAccountWinsWhenASeparateGuestCookieIsPresent()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var account = LinerNotes.Domain.Discovery.ListenerProfile.Registered(userId, now);
        var guest = LinerNotes.Domain.Discovery.ListenerProfile.Guest(now, TimeSpan.FromDays(7));
        var credential = new string('a', 64);
        var hash = ListenerContext.HashCredential(credential)!;
        var http = new DefaultHttpContext();
        http.Request.Headers.Cookie = "liner.guest=" + credential;
        http.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString())], "fixture"));
        var store = Substitute.For<IGuestProfileStore>();
        store.FindAccountAsync(userId, Arg.Any<CancellationToken>()).Returns(account);
        store.FindGuestAsync(hash, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(guest);
        var admission = Substitute.For<IDiscoveryWriteCoordinator>();
        var listener = new ListenerContext(new HttpContextAccessor { HttpContext = http }, store,
            new GuestLifecycle(store, admission, TimeProvider.System), Substitute.For<IAntiforgery>(),
            Microsoft.Extensions.Options.Options.Create(new GuestDiscoveryOptions { Enabled = true }), Substitute.For<IHostEnvironment>(), TimeProvider.System);
        Assert.Same(account, await listener.ResolveAsync(false, default));
        Assert.Same(guest, await listener.GuestAsync(default));
        await store.DidNotReceive().CreateAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ProductionCredentialsRequireHttpsAndUseSecureHttpOnlyCookies()
    {
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString("music.example");
        var accessor = new HttpContextAccessor { HttpContext = http };
        var store = Substitute.For<IGuestProfileStore>();
        var admission = Substitute.For<IDiscoveryWriteCoordinator>();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Production");
        var listener = new ListenerContext(accessor, store, new GuestLifecycle(store, admission, TimeProvider.System),
            Substitute.For<IAntiforgery>(), Microsoft.Extensions.Options.Options.Create(new GuestDiscoveryOptions { Enabled = true, PublicOrigin = "https://music.example" }), environment, TimeProvider.System);
        http.Request.Scheme = "http";
        Assert.Throws<UnauthorizedAccessException>(() => listener.IssueCredential());
        http.Request.Scheme = "https";
        var hash = listener.IssueCredential();
        Assert.Equal(64, hash.Length);
        var header = http.Response.Headers.SetCookie.ToString();
        Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", header, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(hash, header);
    }
}
