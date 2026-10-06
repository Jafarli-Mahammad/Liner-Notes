using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LinerNotes.Presentation.Services;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace LinerNotes.Presentation.Tests;

public class CurrentUserServiceTests
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CurrentUserService _currentUserService;

    public CurrentUserServiceTests()
    {
        _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        _currentUserService = new CurrentUserService(_httpContextAccessor);
    }

    [Fact]
    public void UserId_WhenUserNotAuthenticated_ReturnsEmptyGuid()
    {
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        Assert.Equal(Guid.Empty, _currentUserService.UserId);
        Assert.Null(_currentUserService.Email);
        Assert.False(_currentUserService.IsAuthenticated);
    }

    [Fact]
    public void UserId_WhenUserAuthenticatedWithClaims_ReturnsParsedGuidAndEmail()
    {
        var expectedUserId = Guid.NewGuid();
        var expectedEmail = "listener@example.com";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, expectedUserId.ToString()),
            new(ClaimTypes.Email, expectedEmail)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };
        _httpContextAccessor.HttpContext.Returns(context);

        Assert.Equal(expectedUserId, _currentUserService.UserId);
        Assert.Equal(expectedEmail, _currentUserService.Email);
        Assert.True(_currentUserService.IsAuthenticated);
    }

    [Fact]
    public void UserId_WhenUserHasJwtSubClaim_ReturnsParsedGuid()
    {
        var expectedUserId = Guid.NewGuid();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, expectedUserId.ToString()),
            new(JwtRegisteredClaimNames.Email, "jwt@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var context = new DefaultHttpContext { User = principal };
        _httpContextAccessor.HttpContext.Returns(context);

        Assert.Equal(expectedUserId, _currentUserService.UserId);
        Assert.Equal("jwt@example.com", _currentUserService.Email);
    }
}
