using LinerNotes.Application.Common.Interfaces.Recommendation;
namespace LinerNotes.Application.Common.Interfaces.Discovery;
public interface ICoordinatedGenerationStorage
{
    Task<ICoordinatedStorageLease> AcquireCoordinatedAsync(GenerationStorageState database, CancellationToken ct);
    // Verify the filesystem before taking database-wide admission; namespace writers remain excluded.
    Task<ICoordinatedStorageLease> PrepareCoordinatedAsync(CancellationToken ct);
    // Refresh only a known revision; never establish a new accounting baseline. Caller holds the database admission lease.
    Task RefreshKnownInventoryAsync(GenerationStorageState database, CancellationToken ct);
    Task<IStorageReclamationLease> PrepareReclamationAsync(CancellationToken ct);
    Task<IStorageReclamationLease> AcquireReclamationAsync(GenerationStorageState database, CancellationToken ct);
}
public interface IStorageReclamationLease : IAsyncDisposable
{
    bool UsesCommitWitness => false;
    Task RebaseAsync(GenerationStorageState database, CancellationToken ct);
    Task ApplyDeletedArtifactsAsync(IReadOnlyList<ArtifactPublication> operations, CancellationToken ct) => throw new NotSupportedException();
    Task ApplyDeletedArtifactAsync(ArtifactPublication operation, CancellationToken ct) => throw new NotSupportedException();
    StoragePublicationPlan CreatePublicationPlan();
    void BindCommit(ManagedStorageCommit commit);
    Task VerifyAsync(GenerationStorageState database, CancellationToken ct);
    Task PublishCommittedInventoryAsync(GenerationStorageState committed, CancellationToken ct);
}
public interface ICoordinatedStorageLease : IGenerationStorageLease
{
    bool UsesCommitWitness => false;
    Task ApplyPublishedArtifactAsync(ArtifactPublication operation, IGenerationStorageReservation reservation, CancellationToken ct)
        => throw new NotSupportedException();
    Task RebaseAsync(GenerationStorageState database, CancellationToken ct);
    StoragePublicationPlan CreatePublicationPlan();
    void BindCommit(ManagedStorageCommit commit);
    // After the database commit. Failure leaves a revision mismatch and requires explicit reconciliation.
    Task PublishCommittedInventoryAsync(GenerationStorageState committed, CancellationToken ct);
}
