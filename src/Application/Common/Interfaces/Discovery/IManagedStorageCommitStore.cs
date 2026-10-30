using LinerNotes.Application.Common.Interfaces.Recommendation;

namespace LinerNotes.Application.Common.Interfaces.Discovery;

public sealed record StoragePublicationPlan(string BeforeInventoryJson, string ArtifactCategoriesJson,
    string OwnedChangesJson, string RootFingerprint, DateTimeOffset MeasuredAtUtc);

public sealed record ManagedStorageCommit(Guid CommitId, StoragePublicationPlan Plan,
    string ExpectedRevision, long ExpectedRowCount);

/// <summary>One immutable witness for the latest serialized commit. The witness update accounts for itself.</summary>
public interface IManagedStorageCommitStore
{
    Task<ManagedStorageCommit?> ReadAsync(CancellationToken ct);
    // Must be the last managed mutation in the caller's transaction.
    Task<ManagedStorageCommit> RecordAsync(Guid commitId, StoragePublicationPlan plan, string schemaFingerprint, CancellationToken ct);
    // Explicit operator establishment only, while writers are stopped and filesystem ownership is held.
    Task<GenerationStorageState> EstablishAsync(Guid commitId, StoragePublicationPlan plan, CancellationToken ct);
}
