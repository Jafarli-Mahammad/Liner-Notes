using LinerNotes.Application.Common.Mappings;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.DTOs.Subscribers;
using MediatR;

namespace LinerNotes.Application.Features.Subscribers.Queries.GetSubscriberProfile;

/// <summary>
/// Handler for resolving subscriber profiles by unique ID.
/// </summary>
public sealed class GetSubscriberProfileQueryHandler : IRequestHandler<GetSubscriberProfileQuery, SubscriberDto?>
{
    private readonly IUserRepository _userRepository;

    public GetSubscriberProfileQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<SubscriberDto?> Handle(
        GetSubscriberProfileQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        return user?.ToDto();
    }
}
