using LinerNotes.Application.Common.Interfaces.Recommendation;
namespace LinerNotes.Application.Common.Interfaces.Discovery;
public interface ICoordinatedGenerationStorage
{
    Task<ICoordinatedStorageLease> AcquireCoordinatedAsync(GenerationStorageState database, CancellationToken ct);
    // Refresh only a known revision; never establish a new accounting baseline. Caller holds the database admission lease.
    Task RefreshKnownInventoryAsync(GenerationStorageState database, CancellationToken ct);
    Task<IStorageReclamationLease> AcquireReclamationAsync(GenerationStorageState database, CancellationToken ct);
}
public interface IStorageReclamationLease : IAsyncDisposable
{
    Task VerifyAsync(GenerationStorageState database, CancellationToken ct);
    Task PublishCommittedInventoryAsync(GenerationStorageState committed, CancellationToken ct);
}
public interface ICoordinatedStorageLease : IGenerationStorageLease
{
    // After the database commit. Failure leaves a revision mismatch and requires explicit reconciliation.
    Task PublishCommittedInventoryAsync(GenerationStorageState committed, CancellationToken ct);
}
