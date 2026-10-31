namespace LinerNotes.Application.Common.Interfaces.Discovery;

public enum ArtifactPublicationPhase { Reserved = 0, Prepared = 1, Publishing = 2, Published = 3, Abandoned = 4, Deleting = 5, Deleted = 6, Canceling = 7 }

/// <summary>Durable ownership of one staging name and one final name, with a capacity reservation.</summary>
public sealed record ArtifactPublication(Guid Id, string Category, string Root, string FinalName, string StageName,
    long ReservedBytes, long ContentBytes, string Sha256, ArtifactPublicationPhase Phase, long Version, Guid? OwnerId = null, long LastWriteTimeUtcTicks = 0);

/// <summary>Mutations require the caller's coordinated transaction. State/version transitions are fenced.</summary>
public interface IArtifactPublicationStore
{
    Task<IReadOnlyList<ArtifactPublication>> ReadActiveAsync(CancellationToken ct);
    Task<ArtifactPublication?> FindAsync(Guid id, CancellationToken ct);
    Task<ArtifactPublication?> FindByNameAsync(string root, string finalName, CancellationToken ct);
    Task<IReadOnlyList<ArtifactPublication>> GetOwnerPageAsync(Guid ownerId, Guid? after, int limit, CancellationToken ct);
    Task DeleteTerminalOwnerRowsAsync(Guid ownerId, CancellationToken ct);
    Task RegisterExistingWithinAdmissionAsync(ArtifactPublication operation, CancellationToken ct);
    Task<IReadOnlyList<ArtifactPublication>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    Task<IReadOnlyList<ArtifactPublication>> PrepareDeletionBatchAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    Task DeleteDeletingBatchWithinAdmissionAsync(IReadOnlyList<ArtifactPublication> operations, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetOrphanedOwnersAsync(Guid? after, int limit, CancellationToken ct);
    Task<IReadOnlyList<ArtifactPublication>> GetOwnerCleanupPageAsync(Guid ownerId, int limit, CancellationToken ct);
    Task<bool> OwnerExistsAsync(Guid ownerId, CancellationToken ct);
    Task ReserveAsync(ArtifactPublication operation, CancellationToken ct);
    Task<ArtifactPublication> TransitionAsync(Guid id, long expectedVersion, ArtifactPublicationPhase expectedPhase,
        ArtifactPublicationPhase nextPhase, CancellationToken ct, long? verifiedWriteTimeUtcTicks = null);
}
