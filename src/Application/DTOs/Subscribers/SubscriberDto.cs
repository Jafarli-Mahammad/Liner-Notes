using LinerNotes.Domain.Enums;

namespace LinerNotes.Application.DTOs.Subscribers;

/// <summary>
/// Immutable DTO representing a subscriber's public profile and delivery preferences.
/// </summary>
public record SubscriberDto(
    Guid Id,
    string Email,
    string TimeZone,
    DigestDeliveryDay DeliveryDay,
    int DeliveryHourUtc,
    DateTime? NextDigestAt,
    DateTime CreatedAt);
