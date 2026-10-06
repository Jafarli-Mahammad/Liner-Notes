using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LinerNotes.Presentation.Options;
using LinerNotes.Presentation.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public class JwtServiceTests
{
    private readonly JwtService _jwtService;
    private readonly JwtOptions _options;

    public JwtServiceTests()
    {
        _options = new JwtOptions
        {
            SecretKey = "Super_Secret_Key_For_Testing_Purposes_That_Is_Long_Enough_To_Be_Valid_256_Bits",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            ExpiryMinutes = 60
        };

        _jwtService = new JwtService(Microsoft.Extensions.Options.Options.Create(_options));
    }

    [Fact]
    public void GenerateAccessToken_ReturnsValidJwtStringWithCorrectClaims()
    {
        var userId = Guid.NewGuid();
        var userName = "testlistener";
        var email = "test@example.com";

        var token = _jwtService.GenerateAccessToken(userId, userName, email);

        Assert.False(string.IsNullOrWhiteSpace(token));

        var handler = new JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(token);

        Assert.Equal(_options.Issuer, parsed.Issuer);
        Assert.Contains(_options.Audience, parsed.Audiences);
        Assert.Equal(userId.ToString(), parsed.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(email, parsed.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyCryptographicString()
    {
        var token1 = _jwtService.GenerateRefreshToken();
        var token2 = _jwtService.GenerateRefreshToken();

        Assert.False(string.IsNullOrWhiteSpace(token1));
        Assert.False(string.IsNullOrWhiteSpace(token2));
        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_ExtractsValidClaimsPrincipal()
    {
        var userId = Guid.NewGuid();
        var token = _jwtService.GenerateAccessToken(userId, "testlistener", "test@example.com");

        var principal = _jwtService.GetPrincipalFromExpiredToken(token);

        Assert.NotNull(principal);
        var subClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Assert.Equal(userId.ToString(), subClaim);
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_InvalidSignature_ReturnsNull()
    {
        var invalidToken = "invalid.token.signature";

        var principal = _jwtService.GetPrincipalFromExpiredToken(invalidToken);

        Assert.Null(principal);
    }
}
