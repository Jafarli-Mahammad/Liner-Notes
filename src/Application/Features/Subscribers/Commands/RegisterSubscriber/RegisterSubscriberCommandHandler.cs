using AutoMapper;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Domain.Digest;
using MediatR;

namespace LinerNotes.Application.Features.Subscribers.Commands.RegisterSubscriber;

/// <summary>
/// Handler responsible for creating and persisting subscriber accounts.
/// </summary>
public sealed class RegisterSubscriberCommandHandler : IRequestHandler<RegisterSubscriberCommand, SubscriberDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public RegisterSubscriberCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<SubscriberDto> Handle(
        RegisterSubscriberCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException($"A subscriber with email '{request.Email}' already exists.");
        }

        var user = new User(
            email: request.Email,
            timeZone: request.TimeZone,
            deliveryDay: request.DeliveryDay,
            deliveryHourUtc: request.DeliveryHourUtc,
            id: request.UserId);

        // Calculate initial next digest delivery slot
        var initialDelivery = DateTime.UtcNow.Date.AddDays(7).AddHours(request.DeliveryHourUtc);
        user.SetNextDigestAt(DateTime.SpecifyKind(initialDelivery, DateTimeKind.Utc));

        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<SubscriberDto>(user);
    }
}
