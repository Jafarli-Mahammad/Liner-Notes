using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Presentation.Discovery;
using LinerNotes.Application.Common.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
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
[EnableRateLimiting("auth")]
[ServiceFilter(typeof(LinerNotes.Presentation.Filters.DiscoveryWriteFilter))]
public sealed class AuthController : ApiControllerBase
{
    private const string SessionCookie = "liner.session";
    private readonly IDataProtector? _sessions;
    private readonly IAuthService _authService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtService _jwtService;
    private readonly JwtOptions _jwtOptions;
    private readonly IListenerContext? _listener;
    private readonly IDiscoveryWriteCoordinator? _admission;

    public AuthController(
        IAuthService _authService,
        IJwtService jwtService,
        IOptions<JwtOptions> jwtOptions,
        IUnitOfWork unitOfWork,
        IListenerContext? listener = null,
        IDiscoveryWriteCoordinator? admission = null,
        IDataProtectionProvider? protection = null)
    {
        this._authService = _authService;
        _sessions = protection?.CreateProtector("LinerNotes.BrowserSession.v1");
        _listener = listener;
        _admission = admission;
        _unitOfWork = unitOfWork;
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
        string? guestHash = null;
        if (request.PreserveGuestProgress && _listener?.Enabled == true && _listener.CredentialHash is { } hash)
        {
            await _listener.ValidateMutationAsync(cancellationToken);
            if (await _listener.GuestAsync(cancellationToken) is null) return Unauthorized();
            guestHash = hash;
        }
        Task<IActionResult> WriteAsync(CancellationToken token) => _unitOfWork.ExecuteInTransactionAsync<IActionResult>(async ct =>
        {
            ct.ThrowIfCancellationRequested();
            var userId = await _authService.RegisterAsync(request.UserName, request.Email, request.Password);
            // 2. Register domain subscriber record linked 1:1 by Id
            var registerCommand = new RegisterSubscriberCommand(
                Email: request.Email,
                TimeZone: request.TimeZone,
                DeliveryDay: request.DeliveryDay,
                DeliveryHourUtc: request.DeliveryHourUtc,
                UserId: userId,
                GuestCredentialHash: guestHash,
                WeeklyEmailOptIn: request.WeeklyEmailOptIn);

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
            await _authService.StoreRefreshTokenAsync(subscriber.Id, refreshToken);

            var response = new AuthResponse(
                AccessToken: accessToken,
                RefreshToken: refreshToken,
                ExpiresIn: _jwtOptions.ExpiryMinutes * 60,
                User: subscriber);

            return CreatedAtAction(nameof(Me), response);
        }, token);
        var result = guestHash is not null
            ? await (_admission ?? throw new InvalidOperationException("guest_handoff_unavailable"))
                .ExecuteAsync(131_072, WriteAsync, cancellationToken)
            : await WriteAsync(cancellationToken);
        if (result is CreatedAtActionResult { Value: AuthResponse session }) SaveSession(session);
        if (guestHash is not null) _listener!.RevokeCookie();
        return result;
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
        await _authService.StoreRefreshTokenAsync(userId, refreshToken);

        var response = new AuthResponse(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresIn: _jwtOptions.ExpiryMinutes * 60,
            User: subscriber);

        SaveSession(response);
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

        var isValid = await _authService.ValidateRefreshTokenAsync(userId, request.RefreshToken);
        if (!isValid)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid Refresh Token",
                Detail = "The provided refresh token is invalid, expired, or has been revoked.",
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
        if (!await _authService.RotateRefreshTokenAsync(userId, request.RefreshToken, newRefreshToken, cancellationToken))
            return Unauthorized(new ProblemDetails { Title = "Invalid Refresh Token", Status = StatusCodes.Status401Unauthorized });

        var response = new AuthResponse(
            AccessToken: newAccessToken,
            RefreshToken: newRefreshToken,
            ExpiresIn: _jwtOptions.ExpiryMinutes * 60,
            User: subscriber);

        SaveSession(response);
        return Ok(response);
    }

    // Cookie credentials are accepted only by these same-origin, non-simple POSTs.
    // Normal API requests continue to require bearer tokens.
    [HttpPost("session")]
    [EnableRateLimiting("session")]
    public async Task<IActionResult> RestoreSession(CancellationToken cancellationToken)
    {
        if (Request.Headers["X-Liner-Session"] != "1") return BadRequest();
        var saved = ReadSession();
        if (saved is null) { ClearSession(); return NoContent(); }
        var result = await Refresh(saved, cancellationToken);
        if (result is UnauthorizedObjectResult or UnauthorizedResult or BadRequestObjectResult)
            ClearSession();
        return result;
    }

    [HttpPost("logout")]
    [EnableRateLimiting("session")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Headers["X-Liner-Session"] != "1") return BadRequest();
        var saved = ReadSession();
        if (saved is not null)
        {
            var principal = _jwtService.GetPrincipalFromExpiredToken(saved.AccessToken);
            var claim = principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? principal?.FindFirst("sub")?.Value;
            if (Guid.TryParse(claim, out var userId))
                // Replace only this session; a concurrent new login must survive stale logout.
                await _authService.RotateRefreshTokenAsync(userId, saved.RefreshToken,
                    _jwtService.GenerateRefreshToken(), cancellationToken);
        }
        ClearSession();
        return NoContent();
    }

    private CookieOptions SessionOptions() => new()
    {
        HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Strict,
        Path = "/api/auth", IsEssential = true
    };

    private void SaveSession(AuthResponse response)
    {
        if (_sessions is null) return; // Direct controller tests do not install browser transport.
        var options = SessionOptions();
        options.Expires = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshExpiryDays);
        Response.Cookies.Append(SessionCookie, _sessions.Protect(JsonSerializer.Serialize(
            new RefreshTokenRequest(response.AccessToken, response.RefreshToken))), options);
        var marker = SessionOptions();
        marker.Path = "/"; marker.HttpOnly = false; marker.Expires = options.Expires;
        Response.Cookies.Append("liner.signed-in", "1", marker);
        Response.Headers.CacheControl = "no-store";
    }

    private void ClearSession()
    {
        Response.Cookies.Delete(SessionCookie, SessionOptions());
        var marker = SessionOptions(); marker.Path = "/"; marker.HttpOnly = false;
        Response.Cookies.Delete("liner.signed-in", marker);
    }

    private RefreshTokenRequest? ReadSession()
    {
        if (_sessions is null || !Request.Cookies.TryGetValue(SessionCookie, out var cookie)) return null;
        try { return JsonSerializer.Deserialize<RefreshTokenRequest>(_sessions.Unprotect(cookie)); }
        catch (Exception ex) when (ex is CryptographicException or JsonException) { return null; }
    }

    /// <summary>
    /// Retrieves the profile of the currently authenticated subscriber.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [EnableRateLimiting("session")]
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
