using System.Security.Claims;

namespace LinerNotes.Application.Services;

/// <summary>
/// Service contract for generating and validating JWT tokens and claims principals.
/// </summary>
public interface IJwtService
{
    string GenerateAccessToken(Guid userId, string userName, string email, IEnumerable<Claim>? extraClaims = null);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
