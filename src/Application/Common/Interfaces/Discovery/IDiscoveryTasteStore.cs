using LinerNotes.Domain.Taste;
namespace LinerNotes.Application.Common.Interfaces.Discovery;
public interface IDiscoveryTasteStore
{
    // Called within managed admission. Locks the profile and atomically replaces manual selections only.
    Task<long> ReplaceManualAsync(Guid profileId, long expectedRevision, IReadOnlyList<TasteSignal> signals,
        DateTimeOffset now, CancellationToken ct);
}
