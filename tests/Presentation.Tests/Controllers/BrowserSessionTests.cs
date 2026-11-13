using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Application.Features.Subscribers.Queries.GetSubscriberProfile;
using LinerNotes.Application.Services;
using LinerNotes.Domain.Enums;
using LinerNotes.Presentation.Controllers;
using LinerNotes.Presentation.Models;
using LinerNotes.Presentation.Options;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace LinerNotes.Presentation.Tests.Controllers;

public sealed class BrowserSessionTests
{
    [Fact]
    public async Task LoginIssuesProtectedHttpOnlyPersistentCookieAndNewHostRestoresIt()
    {
        var keys = Path.Combine(Path.GetTempPath(), "liner-session-" + Guid.NewGuid().ToString("N"));
        var auth = Substitute.For<IAuthService>();
        var jwt = Substitute.For<IJwtService>();
        var mediator = Substitute.For<ISender>();
        var id = Guid.NewGuid();
        auth.CheckPasswordAsync("fixture@example.test", "fixture-password").Returns((id, "fixture", "fixture@example.test"));
        var subscriber = new SubscriberDto(id, "fixture@example.test", "UTC", DigestDeliveryDay.Sunday, 8,
            DateTime.UtcNow.AddDays(7), DateTime.UtcNow);
        mediator.Send(Arg.Any<GetSubscriberProfileQuery>(), Arg.Any<CancellationToken>()).Returns(subscriber);
        jwt.GenerateAccessToken(id, "fixture", "fixture@example.test").Returns("access");
        jwt.GenerateRefreshToken().Returns("refresh");
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([
            new(System.Security.Claims.ClaimTypes.NameIdentifier, id.ToString()), new(System.Security.Claims.ClaimTypes.Name, "fixture")], "test"));
        jwt.GetPrincipalFromExpiredToken("access").Returns(principal);
        auth.ValidateRefreshTokenAsync(id, "refresh").Returns(true);
        auth.RotateRefreshTokenAsync(id, "refresh", "refresh", Arg.Any<CancellationToken>()).Returns(true);
        var first = Create(auth, jwt, mediator, keys);
        await first.Login(new LoginRequest("fixture@example.test", "fixture-password"), default);
        var cookies = first.Response.Headers.SetCookie.ToArray();
        var cookie = cookies.Single(c => c!.StartsWith("liner.session="))!;
        Assert.Contains("httponly", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("expires=", cookie);
        Assert.DoesNotContain("refresh", cookie);
        // A fresh Data Protection provider reads keys from disk, as after host restart.
        var restarted = Create(auth, jwt, mediator, keys);
        restarted.Request.Headers.Cookie = cookie.Split(';')[0];
        restarted.Request.Headers["X-Liner-Session"] = "1";
        Assert.IsType<OkObjectResult>(await restarted.RestoreSession(default));
        auth.ValidateRefreshTokenAsync(id, "refresh").Returns(false);
        Assert.IsType<UnauthorizedObjectResult>(await restarted.RestoreSession(default));
        Assert.Contains(restarted.Response.Headers.SetCookie, value => value!.StartsWith("liner.session=;") && value.Contains("expires="));
    }

    [Fact]
    public async Task CookieEndpointsRejectSimpleCrossOriginRequestsAndMalformedCookies()
    {
        var controller = Create(Substitute.For<IAuthService>(), Substitute.For<IJwtService>(), Substitute.For<ISender>(),
            Path.Combine(Path.GetTempPath(), "liner-session-" + Guid.NewGuid().ToString("N")));
        Assert.IsType<BadRequestResult>(await controller.RestoreSession(default));
        Assert.IsType<BadRequestResult>(await controller.Logout(default));
        controller.Request.Headers["X-Liner-Session"] = "1";
        controller.Request.Headers.Cookie = "liner.session=tampered";
        Assert.IsType<NoContentResult>(await controller.RestoreSession(default));
    }

    private static AuthController Create(IAuthService auth, IJwtService jwt, ISender mediator, string keys)
    {
        var services = new ServiceCollection().AddSingleton(mediator).BuildServiceProvider();
        var controller = new AuthController(auth, jwt, Microsoft.Extensions.Options.Options.Create(new JwtOptions()), Substitute.For<IUnitOfWork>(),
            protection: DataProtectionProvider.Create(new DirectoryInfo(keys)));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } };
        controller.Request.Scheme = "https";
        return controller;
    }
}
