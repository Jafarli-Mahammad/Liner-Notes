using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Domain.Digest;

namespace LinerNotes.Application.Common.Interfaces;

public sealed record LocalCaptureInputs(SubscriberDto? Subscriber, WeeklyDigestDto? Digest, GenerationStorageState Storage);
public interface ILocalDigestCaptureStore
{
    Task<LocalCaptureInputs> ReadAsync(Guid userId, IsoWeek week, CancellationToken ct);
    Task<ILocalDigestCaptureTransaction> BeginAsync(Guid userId, IsoWeek week, CancellationToken ct);
    Task RecordFailureAsync(Guid userId, IsoWeek week, string reason, CancellationToken ct);
}
public interface ILocalDigestCaptureTransaction : IAsyncDisposable
{
    LocalCaptureInputs Inputs { get; }
    Task CommitCapturedAsync(CancellationToken ct);
}
public sealed class LocalCaptureTransientException() : Exception("capture_database_unavailable");
