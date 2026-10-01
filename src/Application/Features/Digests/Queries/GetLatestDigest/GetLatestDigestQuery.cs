using LinerNotes.Application.DTOs.Digests;
using MediatR;

namespace LinerNotes.Application.Features.Digests.Queries.GetLatestDigest;

/// <summary>
/// Query to retrieve a subscriber's most recent weekly discovery digest.
/// </summary>
public record GetLatestDigestQuery(Guid UserId) : IRequest<WeeklyDigestDto?>;
