using LinerNotes.Application.Features.Subscribers.Commands.RegisterSubscriber;
using LinerNotes.Application.Features.Subscribers.Queries.GetSubscriberProfile;
using LinerNotes.Application.Features.Taste.Commands.SeedTasteProfile;
using LinerNotes.Application.Services;
using LinerNotes.Presentation.Models;
using LinerNotes.Presentation.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LinerNotes.Presentation.Controllers;

/// <summary>
/// Authentication endpoints for subscriber registration, login, and profile resolution.
/// </summary>
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;
    private readonly IJwtService _jwtService;
    private readonly JwtOptions _jwtOptions;

    public AuthController(
        IAuthService _authService,
        IJwtService jwtService,
        IOptions<JwtOptions> jwtOptions)
    {
        this._authService = _authService;
        _jwtService = jwtService;
        _jwtOptions = jwtOptions.Value;
    }

    /// <summary>
    /// Registers a new subscriber, initializes delivery schedule, and optionally seeds taste profile.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Create Identity credentials
        var userId = await _authService.RegisterAsync(request.UserName, request.Email, request.Password);

        // 2. Register domain subscriber record linked 1:1 by Id
        var registerCommand = new RegisterSubscriberCommand(
            Email: request.Email,
            TimeZone: request.TimeZone,
            DeliveryDay: request.DeliveryDay,
            DeliveryHourUtc: request.DeliveryHourUtc,
            UserId: userId);

        var subscriber = await Mediator.Send(registerCommand, cancellationToken);

        // 3. Optional hybrid onboarding seeding (3-5 tags and/or artists)
        var hasTags = request.SeedTags is not null && request.SeedTags.Count > 0;
        var hasArtists = request.SeedArtists is not null && request.SeedArtists.Count > 0;

        if (hasTags || hasArtists)
        {
            var seedCommand = new SeedTasteProfileCommand(
                UserId: subscriber.Id,
                Tags: request.SeedTags ?? Array.Empty<Application.DTOs.Taste.SeedTagDto>(),
                Artists: request.SeedArtists ?? Array.Empty<Application.DTOs.Taste.SeedArtistDto>());

            await Mediator.Send(seedCommand, cancellationToken);
        }

        // 4. Issue JWT access and refresh tokens
        var accessToken = _jwtService.GenerateAccessToken(subscriber.Id, request.UserName, subscriber.Email);
        var refreshToken = _jwtService.GenerateRefreshToken();

        var response = new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresIn: _jwtOptions.ExpiryMinutes * 60,
            User: subscriber);

        return CreatedAtAction(nameof(Me), response);
    }

    /// <summary>
    /// Authenticates a subscriber with email and password, returning a JWT bearer token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var authResult = await _authService.CheckPasswordAsync(request.Email, request.Password);
        if (authResult is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Authentication Failed",
                Detail = "Invalid email or password.",
                Status = StatusCodes.Status401Unauthorized,
                Instance = HttpContext.Request.Path
            });
        }

        var (userId, userName, email) = authResult.Value;

        var subscriber = await Mediator.Send(new GetSubscriberProfileQuery(userId), cancellationToken);
        if (subscriber is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Subscriber Not Found",
                Detail = "Domain subscriber profile does not exist.",
                Status = StatusCodes.Status404NotFound,
                Instance = HttpContext.Request.Path
            });
        }

        var accessToken = _jwtService.GenerateAccessToken(userId, userName, email);
        var refreshToken = _jwtService.GenerateRefreshToken();

        var response = new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresIn: _jwtOptions.ExpiryMinutes * 60,
            User: subscriber);

        return Ok(response);
    }

    /// <summary>
    /// Refreshes an expired JWT access token using a valid refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AccessToken) || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Bad Request",
                Detail = "Both AccessToken and RefreshToken are required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var principal = _jwtService.GetPrincipalFromExpiredToken(request.AccessToken);
        if (principal is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid Token",
                Detail = "Expired token signature could not be verified.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid Token Claims",
                Detail = "User identifier claim is missing or invalid.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var subscriber = await Mediator.Send(new GetSubscriberProfileQuery(userId), cancellationToken);
        if (subscriber is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Subscriber Not Found",
                Detail = "Subscriber account no longer exists.",
                Status = StatusCodes.Status401Unauthorized
            });
        }

        var userName = principal.Identity?.Name ?? subscriber.Email;
        var newAccessToken = _jwtService.GenerateAccessToken(userId, userName, subscriber.Email);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        var response = new AuthResponse(
            AccessToken: newAccessToken,
            RefreshToken: newRefreshToken,
            ExpiresIn: _jwtOptions.ExpiryMinutes * 60,
            User: subscriber);

        return Ok(response);
    }

    /// <summary>
    /// Retrieves the profile of the currently authenticated subscriber.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(Application.DTOs.Subscribers.SubscriberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var profile = await Mediator.Send(new GetSubscriberProfileQuery(CurrentUser.UserId), cancellationToken);
        if (profile is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Profile Not Found",
                Detail = "Subscriber profile was not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(profile);
    }
}
