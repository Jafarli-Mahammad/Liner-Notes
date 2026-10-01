using MediatR;

namespace LinerNotes.Application.Features.Subscribers.Commands.DeleteUserAccount;

/// <summary>
/// Command to soft-delete and purge a user's account and discovery preferences.
/// </summary>
public record DeleteUserAccountCommand(Guid UserId) : IRequest<bool>;
