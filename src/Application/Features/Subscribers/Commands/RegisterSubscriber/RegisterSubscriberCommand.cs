using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Domain.Enums;
using MediatR;

namespace LinerNotes.Application.Features.Subscribers.Commands.RegisterSubscriber;

/// <summary>
/// Command to register a new music discovery subscriber.
/// </summary>
public record RegisterSubscriberCommand(
    string Email,
    string TimeZone = "UTC",
    DigestDeliveryDay DeliveryDay = DigestDeliveryDay.Sunday,
    int DeliveryHourUtc = 8) : IRequest<SubscriberDto>;
