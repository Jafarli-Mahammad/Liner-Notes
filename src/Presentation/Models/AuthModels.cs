using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Application.DTOs.Taste;
using LinerNotes.Domain.Enums;

namespace LinerNotes.Presentation.Models;

public sealed record RegisterRequest(
    string UserName,
    string Email,
    string Password,
    string TimeZone = "UTC",
    DigestDeliveryDay DeliveryDay = DigestDeliveryDay.Sunday,
    int DeliveryHourUtc = 8,
    IReadOnlyList<SeedTagDto>? SeedTags = null,
    IReadOnlyList<SeedArtistDto>? SeedArtists = null);

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    SubscriberDto User,
    string TokenType = "Bearer");
