using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Application.Features.Subscribers.Commands.RegisterSubscriber;
using LinerNotes.Application.Features.Subscribers.Queries.GetSubscriberProfile;
using LinerNotes.Application.Services;
using LinerNotes.Domain.Enums;
using LinerNotes.Presentation.Controllers;
using LinerNotes.Presentation.Models;
using LinerNotes.Presentation.Options;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace LinerNotes.Presentation.Tests.Controllers;

public class AuthControllerTests
{
    private readonly IAuthService _authService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;
    private readonly ISender _mediator;
    private readonly IOptions<JwtOptions> _jwtOptions;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _authService = Substitute.For<IAuthService>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _unitOfWork.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<IActionResult>>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<IActionResult>>>()(call.Arg<CancellationToken>()));
        _jwtService = Substitute.For<IJwtService>();
        _mediator = Substitute.For<ISender>();

        var options = new JwtOptions
        {
            SecretKey = "Super_Secret_Key_For_Testing_Purposes_That_Is_Long_Enough_To_Be_Valid_256_Bits",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryMinutes = 60
        };
        _jwtOptions = Microsoft.Extensions.Options.Options.Create(options);

        _controller = new AuthController(_authService, _jwtService, _jwtOptions, _unitOfWork);

        var services = new ServiceCollection();
        services.AddSingleton(_mediator);
        var currentUserService = Substitute.For<ICurrentUserService>();
        services.AddSingleton(currentUserService);
        var serviceProvider = services.BuildServiceProvider();

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = serviceProvider }
        };
    }

    [Fact]
    public async Task Register_ValidRequest_CallsAuthServiceAndMediator_ReturnsCreatedAtAction()
    {
        var request = new RegisterRequest(
            UserName: "newlistener",
            Email: "newlistener@example.com",
            Password: "password123",
            TimeZone: "UTC",
            DeliveryDay: DigestDeliveryDay.Sunday,
            DeliveryHourUtc: 8);

        var userId = Guid.NewGuid();
        _authService.RegisterAsync(request.UserName, request.Email, request.Password)
            .Returns(userId);

        var subscriberDto = new SubscriberDto(
            Id: userId,
            Email: request.Email,
            TimeZone: request.TimeZone,
            DeliveryDay: request.DeliveryDay,
            DeliveryHourUtc: request.DeliveryHourUtc,
            NextDigestAt: DateTime.UtcNow.AddDays(7),
            CreatedAt: DateTime.UtcNow);

        _mediator.Send(Arg.Any<RegisterSubscriberCommand>(), Arg.Any<CancellationToken>())
            .Returns(subscriberDto);

        _jwtService.GenerateAccessToken(userId, request.UserName, request.Email)
            .Returns("fake-access-token");
        _jwtService.GenerateRefreshToken()
            .Returns("fake-refresh-token");

        var result = await _controller.Register(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<AuthResponse>(createdResult.Value);
        Assert.Equal("fake-access-token", response.AccessToken);
        Assert.Equal("fake-refresh-token", response.RefreshToken);
        Assert.Equal(request.Email, response.User.Email);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithTokens()
    {
        var request = new LoginRequest("user@example.com", "validPass123");
        var userId = Guid.NewGuid();

        _authService.CheckPasswordAsync(request.Email, request.Password)
            .Returns((userId, "validuser", request.Email));

        var subscriberDto = new SubscriberDto(
            Id: userId,
            Email: request.Email,
            TimeZone: "UTC",
            DeliveryDay: DigestDeliveryDay.Sunday,
            DeliveryHourUtc: 8,
            NextDigestAt: null,
            CreatedAt: DateTime.UtcNow);

        _mediator.Send(Arg.Is<GetSubscriberProfileQuery>(q => q.UserId == userId), Arg.Any<CancellationToken>())
            .Returns(subscriberDto);

        _jwtService.GenerateAccessToken(userId, "validuser", request.Email)
            .Returns("jwt-token-123");
        _jwtService.GenerateRefreshToken()
            .Returns("refresh-token-123");

        var result = await _controller.Login(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(okResult.Value);
        Assert.Equal("jwt-token-123", response.AccessToken);
        Assert.Equal("refresh-token-123", response.RefreshToken);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var request = new LoginRequest("user@example.com", "wrongPassword");

        _authService.CheckPasswordAsync(request.Email, request.Password)
            .Returns(((Guid, string, string)?)null);

        var result = await _controller.Login(request, CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
    }

    [Fact]
    public async Task Register_WhenDomainRegistrationFails_PropagatesThroughTransaction()
    {
        var request = new RegisterRequest(
            UserName: "failinguser",
            Email: "fail@example.com",
            Password: "password123",
            TimeZone: "UTC",
            DeliveryDay: DigestDeliveryDay.Sunday,
            DeliveryHourUtc: 8);

        var userId = Guid.NewGuid();
        _authService.RegisterAsync(request.UserName, request.Email, request.Password)
            .Returns(userId);

        _mediator.Send(Arg.Any<RegisterSubscriberCommand>(), Arg.Any<CancellationToken>())
            .Returns<SubscriberDto>(_ => throw new InvalidOperationException("Database constraint error"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _controller.Register(request, CancellationToken.None));

        await _unitOfWork.Received(1).ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<IActionResult>>>(), Arg.Any<CancellationToken>());
        await _authService.DidNotReceive().StoreRefreshTokenAsync(Arg.Any<Guid>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Refresh_ValidRefreshToken_RotatesAndReturnsOk()
    {
        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "testuser")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);

        _jwtService.GetPrincipalFromExpiredToken("valid-expired-access-token")
            .Returns(principal);

        _authService.ValidateRefreshTokenAsync(userId, "valid-refresh-token")
            .Returns(true);

        var subscriberDto = new SubscriberDto(
            Id: userId,
            Email: "test@example.com",
            TimeZone: "UTC",
            DeliveryDay: DigestDeliveryDay.Sunday,
            DeliveryHourUtc: 8,
            NextDigestAt: null,
            CreatedAt: DateTime.UtcNow);

        _mediator.Send(Arg.Is<GetSubscriberProfileQuery>(q => q.UserId == userId), Arg.Any<CancellationToken>())
            .Returns(subscriberDto);

        _jwtService.GenerateAccessToken(userId, "testuser", "test@example.com")
            .Returns("new-access-token");
        _jwtService.GenerateRefreshToken()
            .Returns("new-refresh-token");

        _authService.RotateRefreshTokenAsync(userId, "valid-refresh-token", "new-refresh-token", Arg.Any<CancellationToken>()).Returns(true);
        var request = new RefreshTokenRequest("valid-expired-access-token", "valid-refresh-token");
        var result = await _controller.Refresh(request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(okResult.Value);
        Assert.Equal("new-access-token", response.AccessToken);
        Assert.Equal("new-refresh-token", response.RefreshToken);

        await _authService.Received(1).RotateRefreshTokenAsync(userId, "valid-refresh-token", "new-refresh-token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_InvalidRefreshToken_ReturnsUnauthorized()
    {
        var userId = Guid.NewGuid();
        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
        var principal = new System.Security.Claims.ClaimsPrincipal(identity);

        _jwtService.GetPrincipalFromExpiredToken("valid-expired-access-token")
            .Returns(principal);

        _authService.ValidateRefreshTokenAsync(userId, "invalid-refresh-token")
            .Returns(false);

        var request = new RefreshTokenRequest("valid-expired-access-token", "invalid-refresh-token");
        var result = await _controller.Refresh(request, CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(unauthorizedResult.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
    }
}
