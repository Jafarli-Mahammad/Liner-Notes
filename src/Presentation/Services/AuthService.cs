using LinerNotes.Application.Services;
using LinerNotes.DataAccess.IdentityEntities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Presentation.Options;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LinerNotes.Presentation.Services;

/// <summary>
/// Implementation of IAuthService backed by ASP.NET Core Identity.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly DataContext _context;
    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;
    private const string TokenProvider = "LinerNotes";
    private const string TokenName = "RefreshToken";
    private sealed record RefreshSession(int Version, string Hash, DateTimeOffset ExpiresAt, string SecurityStamp);
    private readonly RoleManager<IdentityRole<Guid>>? _roleManager;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        DataContext context,
        IOptions<JwtOptions> options,
        TimeProvider clock,
        RoleManager<IdentityRole<Guid>>? roleManager = null)
    {
        _userManager = userManager;
        _context = context;
        _options = options.Value;
        _clock = clock;
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
            EmailConfirmed = false,
            LockoutEnabled = true,
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

        return await VerifyPasswordAsync(user, password);
    }

    public async Task<(Guid UserId, string UserName, string Email)?> CheckPasswordByUserNameAsync(string userName, string password)
    {
        var user = await _userManager.FindByNameAsync(userName);
        if (user is null)
        {
            return null;
        }

        return await VerifyPasswordAsync(user, password);
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

    private async Task<(Guid UserId, string UserName, string Email)?> VerifyPasswordAsync(ApplicationUser user, string password)
    {
        if (user.IsDeleted || await _userManager.IsLockedOutAsync(user)) return null;
        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            // Fail closed even when another request has changed the Identity concurrency stamp.
            await _userManager.AccessFailedAsync(user);
            return null;
        }
        if (!(await _userManager.ResetAccessFailedCountAsync(user)).Succeeded) return null;
        return (user.Id, user.UserName ?? user.Email!, user.Email!);
    }

    public async Task StoreRefreshTokenAsync(Guid userId, string refreshToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("Account does not exist.");
        var value = SerializeSession(refreshToken, user.SecurityStamp!);
        var result = await _userManager.SetAuthenticationTokenAsync(user, TokenProvider, TokenName, value);
        if (!result.Succeeded) throw new InvalidOperationException("Session could not be stored. Sign in again.");
    }

    public async Task<bool> ValidateRefreshTokenAsync(Guid userId, string refreshToken)
        => await ReadValidSessionAsync(userId, refreshToken, CancellationToken.None) is not null;

    public async Task<bool> RotateRefreshTokenAsync(Guid userId, string refreshToken, string replacementToken,
        CancellationToken cancellationToken = default)
    {
        var current = await ReadValidSessionAsync(userId, refreshToken, cancellationToken);
        if (current is null) return false;
        var (value, stamp) = current.Value;
        var replacement = SerializeSession(replacementToken, stamp);
        // Compare-and-swap is a single database statement: only one concurrent redemption wins.
        var changed = await _context.UserTokens
            .Where(t => t.UserId == userId && t.LoginProvider == TokenProvider && t.Name == TokenName && t.Value == value)
            .Where(t => _context.ApplicationUsers.Any(u => u.Id == userId && u.SecurityStamp == stamp))
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.Value, replacement), cancellationToken);
        return changed == 1;
    }

    private string SerializeSession(string token, string stamp)
        => JsonSerializer.Serialize(new RefreshSession(1, HashToken(token),
            _clock.GetUtcNow().AddDays(_options.RefreshExpiryDays), stamp));

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task<(string Value, string Stamp)?> ReadValidSessionAsync(Guid userId, string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 512) return null;
        var value = await _context.UserTokens.AsNoTracking()
            .Where(t => t.UserId == userId && t.LoginProvider == TokenProvider && t.Name == TokenName)
            .Select(t => t.Value).SingleOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(value)) return null;
        RefreshSession? session;
        try { session = JsonSerializer.Deserialize<RefreshSession>(value); }
        catch (JsonException) { return null; } // Legacy plaintext tokens require a new login.
        if (session is null || session.Version != 1 || session.ExpiresAt <= _clock.GetUtcNow() ||
            session.Hash is null || session.Hash.Length != 64 || string.IsNullOrEmpty(session.SecurityStamp)) return null;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(session.Hash), Encoding.UTF8.GetBytes(HashToken(token)))) return null;
        var active = await _context.ApplicationUsers.AnyAsync(u => u.Id == userId && !u.IsDeleted && u.SecurityStamp == session.SecurityStamp, ct)
            && await _context.Users.AnyAsync(u => u.Id == userId, ct);
        return active ? (value, session.SecurityStamp) : null;
    }

    public async Task<bool> DeleteUserAsync(Guid userId)
    {
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Credential deletion requires the account transaction.");
        // ApplicationUser also implements IAuditableEntity: UserManager.DeleteAsync would soft-delete it.
        return await _context.ApplicationUsers.Where(u => u.Id == userId).ExecuteDeleteAsync() == 1;
    }
}
