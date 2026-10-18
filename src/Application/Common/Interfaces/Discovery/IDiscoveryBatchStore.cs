using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;

namespace LinerNotes.Application.Common.Interfaces.Discovery;

public sealed record DiscoveryInputs(ListenerProfile Profile, IReadOnlyList<TasteSignal> Signals,
    IReadOnlyList<WeeklyRecommendation> Feedback);
public interface IDiscoveryBatchStore
{
    Task<long> SaveFeedbackAsync(Guid profileId, Guid recommendationId, long expectedRevision,
        LinerNotes.Domain.Enums.UserFeedback feedback, int? rating, string? comment, DateTimeOffset now, CancellationToken ct);
    Task<DiscoveryInputs?> ReadInputsAsync(Guid profileId, DateTimeOffset now, int feedbackWindow, CancellationToken ct);
    Task<IReadOnlySet<string>> FindExcludedAsync(Guid profileId, IReadOnlyList<string> aliases, CancellationToken ct);
    Task CompleteAsync(Guid jobId, Guid leaseToken, Guid batchId, DiscoveryBatchKind kind,
        IReadOnlyList<BaselineAPick> picks, string coverageJson, DateTimeOffset? evidenceExpiry,
        DateTimeOffset now, CancellationToken ct);
}
