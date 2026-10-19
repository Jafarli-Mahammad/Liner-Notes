using LinerNotes.Application.DTOs.Export;
using LinerNotes.Domain.Discovery;

namespace LinerNotes.Application.Common.Interfaces.Discovery;

public interface IGuestProfileStore
{
    Task<ListenerProfile?> FindGuestAsync(string credentialHash, DateTimeOffset now, CancellationToken ct);
    Task<ListenerProfile?> FindAccountAsync(Guid userId, CancellationToken ct);
    Task<ListenerProfile> CreateAsync(string credentialHash, DateTimeOffset now, TimeSpan inactivity, CancellationToken ct);
    Task TouchAsync(Guid profileId, DateTimeOffset now, TimeSpan inactivity, TimeSpan minimumInterval, CancellationToken ct);
    Task<DiscoveryExportDto> ExportAsync(Guid profileId, CancellationToken ct);
    Task DeleteGuestAsync(string credentialHash, DateTimeOffset now, CancellationToken ct);
    Task AttachNewAccountAsync(string credentialHash, Guid userId, DateTimeOffset now, CancellationToken ct);
    Task ImportIntoAccountAsync(string credentialHash, Guid userId, DateTimeOffset now, CancellationToken ct);
    Task<int> DeleteExpiredAsync(DateTimeOffset now, int limit, CancellationToken ct);
}

/// <summary>Short, coordinated managed-write admission; must never wrap provider I/O.</summary>
public interface IDiscoveryWriteCoordinator
{
    Task<T> ExecuteAsync<T>(long estimatedBytes, Func<CancellationToken, Task<T>> write, CancellationToken ct);
    // For deletion only. The transaction must not increase the tracked logical byte total.
    Task<T> ReclaimAsync<T>(Func<CancellationToken, Task<T>> delete, CancellationToken ct);
}
