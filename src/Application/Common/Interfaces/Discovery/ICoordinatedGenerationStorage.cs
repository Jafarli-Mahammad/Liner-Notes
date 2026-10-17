using LinerNotes.Application.Common.Interfaces.Recommendation;
namespace LinerNotes.Application.Common.Interfaces.Discovery;
public interface ICoordinatedGenerationStorage
{
    Task<ICoordinatedStorageLease> AcquireCoordinatedAsync(GenerationStorageState database, CancellationToken ct);
}
public interface ICoordinatedStorageLease : IGenerationStorageLease
{
    // After the database commit. Failure leaves a revision mismatch and requires explicit reconciliation.
    Task PublishCommittedInventoryAsync(GenerationStorageState committed, CancellationToken ct);
}
