using LinerNotes.Application.DTOs.Subscribers;
using MediatR;

namespace LinerNotes.Application.Features.Subscribers.Queries.GetSubscriberProfile;

/// <summary>
/// Query to retrieve a subscriber's public profile.
/// </summary>
public record GetSubscriberProfileQuery(Guid UserId) : IRequest<SubscriberDto?>;
