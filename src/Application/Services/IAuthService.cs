namespace LinerNotes.Application.Services;

/// <summary>
/// Authentication service contract for subscriber registration, password verification, and credentials management.
/// </summary>
public interface IAuthService
{
    Task<Guid> RegisterAsync(string userName, string email, string password);
    Task<(Guid UserId, string UserName, string Email)?> CheckPasswordAsync(string email, string password);
    Task<(Guid UserId, string UserName, string Email)?> CheckPasswordByUserNameAsync(string userName, string password);
    Task<(Guid UserId, string UserName, string Email)?> GetUserInfoByEmailAsync(string email);
    Task<(Guid UserId, string UserName, string Email)?> GetUserInfoByNameAsync(string userName);
    Task<(bool Succeeded, string[] Errors)> ResetPasswordAsync(string email, string token, string newPassword);
    Task<bool> AddToRoleAsync(Guid userId, string role);
    Task<bool> ValidateRefreshTokenAsync(Guid userId, string refreshToken);
    Task StoreRefreshTokenAsync(Guid userId, string refreshToken);
    Task<bool> RotateRefreshTokenAsync(Guid userId, string refreshToken, string replacementToken, CancellationToken cancellationToken = default);
    Task<bool> DeleteUserAsync(Guid userId);
}
