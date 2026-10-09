using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Domain.Digest;
using MediatR;

namespace LinerNotes.Application.Features.Digests.Commands.GenerateDigest;

public sealed record GenerateDigestCommand(Guid UserId, IsoWeek Week) : IRequest<GenerateDigestResult>;
public sealed record GenerateDigestResult(string Status, WeeklyDigestDto? Digest, IReadOnlyList<CoverageGap> Gaps,
    string? Reason = null);
