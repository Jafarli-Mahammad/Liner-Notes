using LinerNotes.Application.Common.Mappings;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Application.DTOs.Digests;
using MediatR;

namespace LinerNotes.Application.Features.Digests.Queries.GetLatestDigest;

/// <summary>
/// Handler retrieving the latest weekly digest and recommendation details.
/// </summary>
public sealed class GetLatestDigestQueryHandler : IRequestHandler<GetLatestDigestQuery, WeeklyDigestDto?>
{
    private readonly IWeeklyDigestRepository _weeklyDigestRepository;

    public GetLatestDigestQueryHandler(IWeeklyDigestRepository weeklyDigestRepository)
    {
        _weeklyDigestRepository = weeklyDigestRepository;
    }

    public async Task<WeeklyDigestDto?> Handle(
        GetLatestDigestQuery request,
        CancellationToken cancellationToken)
    {
        var digests = await _weeklyDigestRepository.GetRecentDigestsForUserAsync(request.UserId, 1, cancellationToken);
        var latest = digests.FirstOrDefault();

        return latest?.ToDto();
    }
}
