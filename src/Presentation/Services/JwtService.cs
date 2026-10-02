using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LinerNotes.Application.Services;
using LinerNotes.Presentation.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LinerNotes.Presentation.Services;

/// <summary>
/// Service responsible for generating, validating, and extracting claims from JWT tokens.
/// </summary>
public sealed class JwtService : IJwtService
{
    private readonly JwtOptions _jwtOptions;

    public JwtService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public string GenerateAccessToken(
        Guid userId,
        string userName,
        string email,
        IEnumerable<Claim>? extraClaims = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, userName),
            new(JwtRegisteredClaimNames.UniqueName, userName),
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (extraClaims is not null)
        {
            claims.AddRange(extraClaims);
        }

        var expires = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = !string.IsNullOrWhiteSpace(_jwtOptions.Audience),
            ValidAudience = _jwtOptions.Audience,
            ValidateIssuer = !string.IsNullOrWhiteSpace(_jwtOptions.Issuer),
            ValidIssuer = _jwtOptions.Issuer,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey)),
            ValidateLifetime = false // Allow reading expired token claims for refresh flow
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
