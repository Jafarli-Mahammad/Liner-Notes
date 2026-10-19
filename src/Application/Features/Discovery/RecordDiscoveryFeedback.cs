using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Domain.Enums;
using MediatR;

namespace LinerNotes.Application.Features.Discovery;

public sealed record RecordDiscoveryFeedback(Guid ProfileId, Guid RecommendationId, long Revision,
    UserFeedback Feedback, int? Rating = null, string? Comment = null) : IRequest<long>;
public sealed class RecordDiscoveryFeedbackHandler(IDiscoveryBatchStore store, IDiscoveryWriteCoordinator admission, TimeProvider clock)
    : IRequestHandler<RecordDiscoveryFeedback, long>
{
    public Task<long> Handle(RecordDiscoveryFeedback request, CancellationToken ct)
    {
        if (request.ProfileId == Guid.Empty || request.RecommendationId == Guid.Empty || request.Revision < 0 ||
            !Enum.IsDefined(request.Feedback) || request.Rating is < 1 or > 10 || request.Comment?.Length > 1000)
            throw new ArgumentException("invalid_discovery_feedback");
        return admission.ExecuteAsync(65_536, token => store.SaveFeedbackAsync(request.ProfileId, request.RecommendationId,
            request.Revision, request.Feedback, request.Rating, request.Comment, clock.GetUtcNow(), token), ct);
    }
}
