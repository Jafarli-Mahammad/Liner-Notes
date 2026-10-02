using LinerNotes.Application.Services;
using LinerNotes.DataAccess.IdentityEntities;
using Microsoft.AspNetCore.Identity;

namespace LinerNotes.Presentation.Services;

/// <summary>
/// Implementation of IAuthService backed by ASP.NET Core Identity.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole<Guid>>? _roleManager;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>>? roleManager = null)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<Guid> RegisterAsync(string userName, string email, string password)
    {
        var existingEmail = await _userManager.FindByEmailAsync(email);
        if (existingEmail is not null)
        {
            throw new InvalidOperationException($"User with email '{email}' already exists.");
        }

        var existingName = await _userManager.FindByNameAsync(userName);
        if (existingName is not null)
        {
            throw new InvalidOperationException($"User with username '{userName}' already exists.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Registration failed: {errors}");
        }

        return user.Id;
    }

    public async Task<(Guid UserId, string UserName, string Email)?> CheckPasswordAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return null;
        }

        var isValid = await _userManager.CheckPasswordAsync(user, password);
        return isValid ? (user.Id, user.UserName ?? user.Email!, user.Email!) : null;
    }

    public async Task<(Guid UserId, string UserName, string Email)?> CheckPasswordByUserNameAsync(string userName, string password)
    {
        var user = await _userManager.FindByNameAsync(userName);
        if (user is null)
        {
            return null;
        }

        var isValid = await _userManager.CheckPasswordAsync(user, password);
        return isValid ? (user.Id, user.UserName!, user.Email ?? string.Empty) : null;
    }

    public async Task<(Guid UserId, string UserName, string Email)?> GetUserInfoByEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user is null ? null : (user.Id, user.UserName ?? user.Email!, user.Email!);
    }

    public async Task<(Guid UserId, string UserName, string Email)?> GetUserInfoByNameAsync(string userName)
    {
        var user = await _userManager.FindByNameAsync(userName);
        return user is null ? null : (user.Id, user.UserName!, user.Email ?? string.Empty);
    }

    public async Task<(bool Succeeded, string[] Errors)> ResetPasswordAsync(string email, string token, string newPassword)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return (false, new[] { "User not found." });
        }

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        return (result.Succeeded, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<bool> AddToRoleAsync(Guid userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }

        if (_roleManager is not null && !await _roleManager.RoleExistsAsync(role))
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var result = await _userManager.AddToRoleAsync(user, role);
        return result.Succeeded;
    }
}
