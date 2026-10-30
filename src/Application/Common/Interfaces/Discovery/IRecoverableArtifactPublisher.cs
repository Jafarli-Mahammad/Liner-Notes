namespace LinerNotes.Application.Common.Interfaces.Discovery;

public interface IRecoverableArtifactPublisher
{
    Task<ArtifactPublication?> FindAsync(string root, string finalName, CancellationToken ct);
    Task<ArtifactPublication> StageAsync(string category, string root, string finalName, ReadOnlyMemory<byte> content, CancellationToken ct, Guid? ownerId = null);
    Task<ArtifactPublication> PublishAsync(ArtifactPublication operation, CancellationToken ct);
    Task CompleteWithinAdmissionAsync(ArtifactPublication operation, CancellationToken ct);
    Task<ArtifactPublication> AdoptExistingAsync(string category, string root, string finalName, Guid ownerId, ReadOnlyMemory<byte> authenticatedContent, CancellationToken ct);
    Task DeleteManyAsync(IReadOnlyList<ArtifactPublication> operations, CancellationToken ct);
    Task DeleteAsync(ArtifactPublication operation, CancellationToken ct);
    Task RecoverAsync(CancellationToken ct, Guid? operationId = null);
}
