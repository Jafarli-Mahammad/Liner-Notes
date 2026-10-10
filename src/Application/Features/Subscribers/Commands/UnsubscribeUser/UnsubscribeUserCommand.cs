using LinerNotes.Application.Common.Interfaces;
using MediatR;

namespace LinerNotes.Application.Features.Subscribers.Commands.UnsubscribeUser;

public sealed record UnsubscribeUserCommand(Guid UserId) : IRequest;

public sealed class UnsubscribeUserCommandHandler(IUnsubscribeStore store, TimeProvider clock)
    : IRequestHandler<UnsubscribeUserCommand>
{
    public Task Handle(UnsubscribeUserCommand request, CancellationToken cancellationToken) =>
        store.UnsubscribeAsync(request.UserId, clock.GetUtcNow().UtcDateTime, cancellationToken);
}
