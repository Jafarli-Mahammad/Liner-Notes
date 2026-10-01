namespace LinerNotes.Application.Services;

/// <summary>
/// Service contract providing access to the current authenticated context.
/// </summary>
public interface ICurrentUserService
{
    Guid UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
}
