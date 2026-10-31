using System.Threading.RateLimiting;
using LinerNotes.Application.Common.Interfaces.Recommendation;

namespace LinerNotes.Infrastructure.Storage;

/// <summary>Bound preflight CPU, descriptors and waiting request state before database admission.</summary>
public sealed class StorageAdmissionGate : IDisposable
{
    internal static StorageAdmissionGate Fallback { get; } = new();
    private readonly ConcurrencyLimiter limiter = new(new()
    { PermitLimit = 4, QueueLimit = 32, QueueProcessingOrder = QueueProcessingOrder.OldestFirst });
    public async Task<RateLimitLease> AcquireAsync(CancellationToken ct)
    {
        var lease = await limiter.AcquireAsync(1, ct);
        if (lease.IsAcquired) return lease;
        lease.Dispose(); throw new GenerationStoppedException("storage_busy");
    }
    public void Dispose() => limiter.Dispose();
}
