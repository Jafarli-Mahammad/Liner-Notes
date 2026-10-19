using LinerNotes.Domain.Discovery;
namespace LinerNotes.Application.Common.Interfaces.Discovery;
public enum DiscoveryJobLane { Prepared, Acquisition }
public interface IDiscoveryJobStore
{
    Task<bool> HasReadyAsync(DiscoveryJobKind kind, DiscoveryJobLane lane, DateTimeOffset now, int maximumAttempts, CancellationToken ct);
    Task<DiscoveryJob?> FindAsync(Guid profileId, Guid jobId, DateTimeOffset now, CancellationToken ct);
    Task<DiscoveryJob?> FindByKeyAsync(Guid profileId, string key, CancellationToken ct);
    Task<DiscoveryJob> EnqueueAsync(Guid profileId, string key, string hash, DiscoveryJobKind kind, long revision,
        string payloadJson, DateTimeOffset now, int maximumPending, TimeSpan cooldown, CancellationToken ct);
    Task<DiscoveryJob?> LeaseNextAsync(DiscoveryJobKind kind, DateTimeOffset now, TimeSpan duration, int maximumAttempts, CancellationToken ct,
        DiscoveryJobLane? lane = null);
    Task DeferAsync(Guid jobId, Guid leaseToken, DateTimeOffset now, TimeSpan delay, string progress, CancellationToken ct);
    Task RenewAsync(Guid jobId, Guid leaseToken, DateTimeOffset now, TimeSpan duration, string progress, CancellationToken ct);
    Task FailAsync(Guid jobId, Guid leaseToken, DateTimeOffset now, string code, bool retryable, TimeSpan delay, int maximumAttempts, CancellationToken ct);
    Task<DiscoveryJob> LockLeasedAsync(Guid jobId, Guid leaseToken, DateTimeOffset now, CancellationToken ct);
    Task SaveCompletedAsync(DiscoveryJob job, Guid leaseToken, DateTimeOffset now, Guid? batchId, string? resultJson, CancellationToken ct);
}
public sealed class DiscoveryRequestConflictException(string code) : Exception(code);
public sealed class DiscoveryBusyException(string code, TimeSpan retryAfter) : Exception(code)
{ public TimeSpan RetryAfter { get; } = retryAfter; }
