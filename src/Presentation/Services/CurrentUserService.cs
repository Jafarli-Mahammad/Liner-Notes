using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LinerNotes.Application.Services;
using Microsoft.AspNetCore.Http;

namespace LinerNotes.Presentation.Services;

/// <summary>
/// Provides access to the current authenticated user context from HttpContext.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user is null)
            {
                return Guid.Empty;
            }

            var idClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? user.FindFirst("sub")?.Value;

            return Guid.TryParse(idClaim, out var parsedId) ? parsedId : Guid.Empty;
        }
    }

    public string? Email
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst(ClaimTypes.Email)?.Value
                ?? user?.FindFirst(JwtRegisteredClaimNames.Email)?.Value
                ?? user?.FindFirst("email")?.Value;
        }
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
