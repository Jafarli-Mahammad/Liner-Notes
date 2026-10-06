using LinerNotes.Application.Common.Exceptions;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Domain.Digest;
using MediatR;

namespace LinerNotes.Application.Features.Subscribers.Commands.DeleteUserAccount;

/// <summary>
/// Removes account-owned domain records within the account deletion transaction.
/// </summary>
public sealed class DeleteUserAccountCommandHandler : IRequestHandler<DeleteUserAccountCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteUserAccountCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(
        DeleteUserAccountCommand request,
        CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            if (!await _userRepository.DeleteOwnedDataAsync(request.UserId, ct))
                throw new NotFoundException(nameof(User), request.UserId);
            return true;
        }, cancellationToken);
    }
}
