using System.Security.Cryptography;
using System.Diagnostics;
using LinerNotes.Application.Common.Interfaces;
using System.Threading.RateLimiting;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;

namespace LinerNotes.Infrastructure.Storage;

/// <summary>Capacity reservation precedes filesystem creation. Durable intent precedes rename; business completion follows it.</summary>
public sealed class RecoverableArtifactPublisher(IArtifactPublicationStore operations, IDiscoveryWriteCoordinator coordinator,
    LocalGenerationStorage storage, GenerationStorageOptions options, IArtifactStagingWriter? writer = null, IAccountEmailLease? accountOwnership = null) : IRecoverableArtifactPublisher
{
    private static readonly object RecoveryCursorLock = new();
    private static Guid? recoveryCursor;
    private static readonly ConcurrencyLimiter Work = new(new ConcurrencyLimiterOptions
    {
        PermitLimit = 2, QueueLimit = 16, QueueProcessingOrder = QueueProcessingOrder.OldestFirst
    });
    public Task<ArtifactPublication?> FindAsync(string root, string finalName, CancellationToken ct) => operations.FindByNameAsync(root, finalName, ct);

    public async Task<ArtifactPublication> StageAsync(string category, string root, string finalName, ReadOnlyMemory<byte> content, CancellationToken ct, Guid? ownerId = null)
    {
        using var measurement = LinerNotes.Application.Common.Diagnostics.DiscoveryDiagnostics.Measure("storage.artifact_stage");
        using var permit = await Work.AcquireAsync(1, ct);
        if (!permit.IsAcquired) throw new GenerationStoppedException("artifact_workers_busy");
        Guid id = Guid.NewGuid();
        var operation = new ArtifactPublication(id, category, root, finalName, id.ToString("N") + ".staged",
            content.Length, content.Length, Convert.ToHexStringLower(SHA256.HashData(content.Span)), ArtifactPublicationPhase.Reserved, 0, ownerId);
        storage.ValidatePublicationPaths(operation);
        await using var ownership = await AcquireAsync(id, ct);
        // Reservation and its exact row charge are committed before the first staging byte exists.
        await coordinator.ExecuteAsync(content.Length, async token =>
        {
            if (File.Exists(Path.Combine(root, finalName))) throw new GenerationStoppedException("artifact_unexpected_final");
            await operations.ReserveAsync(operation, token);
            return true;
        }, ct);
        await DurableArtifactFile.StageAsync(root, operation.StageName, content, ct, writer);
        await LocalGenerationStorage.VerifyPublicationContentAsync(Path.Combine(root, operation.StageName), operation, ct);
        return await coordinator.ExecuteAsync(65_536, token => operations.TransitionAsync(id, 0,
            ArtifactPublicationPhase.Reserved, ArtifactPublicationPhase.Prepared, token, File.GetLastWriteTimeUtc(Path.Combine(root, operation.StageName)).Ticks), ct);
    }

    public async Task<ArtifactPublication> PublishAsync(ArtifactPublication operation, CancellationToken ct)
    {
        using var measurement = LinerNotes.Application.Common.Diagnostics.DiscoveryDiagnostics.Measure("storage.artifact_publish");
        await using var ownership = await AcquireAsync(operation.Id, ct);
        operation = await operations.FindAsync(operation.Id, ct) ?? throw new GenerationStoppedException("artifact_publication_missing");
        storage.ValidatePublicationPaths(operation);
        if (operation.Phase == ArtifactPublicationPhase.Published)
        {
            await LocalGenerationStorage.VerifyPublicationContentAsync(Path.Combine(operation.Root, operation.FinalName), operation, ct);
            return operation;
        }
        if (operation.Phase == ArtifactPublicationPhase.Prepared)
            operation = await coordinator.ExecuteAsync(65_536, token => operations.TransitionAsync(operation.Id, operation.Version,
                ArtifactPublicationPhase.Prepared, ArtifactPublicationPhase.Publishing, token), ct);
        if (operation.Phase != ArtifactPublicationPhase.Publishing) throw new GenerationStoppedException("artifact_publication_not_prepared");
        string stage = Path.Combine(operation.Root, operation.StageName), final = Path.Combine(operation.Root, operation.FinalName);
        if (File.Exists(stage))
        {
            await LocalGenerationStorage.VerifyPublicationContentAsync(stage, operation, ct);
            ct.ThrowIfCancellationRequested();
            await using var publication = await storage.AcquirePublicationNamespaceAsync(ct);
            DurableArtifactFile.Publish(operation.Root, operation.StageName, operation.FinalName);
        }
        else await LocalGenerationStorage.VerifyPublicationContentAsync(final, operation, ct);
        return operation;
    }

    public async Task CompleteWithinAdmissionAsync(ArtifactPublication operation, CancellationToken ct)
    {
        await operations.TransitionAsync(operation.Id, operation.Version, ArtifactPublicationPhase.Publishing, ArtifactPublicationPhase.Published, ct);
    }

    public async Task<ArtifactPublication> AdoptExistingAsync(string category, string root, string finalName,
        Guid ownerId, ReadOnlyMemory<byte> authenticatedContent, CancellationToken ct)
    {
        Guid id = Guid.NewGuid();
        var operation = new ArtifactPublication(id, category, root, finalName, id.ToString("N") + ".staged",
            authenticatedContent.Length, authenticatedContent.Length,
            Convert.ToHexStringLower(SHA256.HashData(authenticatedContent.Span)), ArtifactPublicationPhase.Published, 3,
            ownerId, File.GetLastWriteTimeUtc(Path.Combine(root, finalName)).Ticks);
        storage.ValidatePublicationPaths(operation);
        return await coordinator.ExecuteAsync(65_536, async token =>
        {
            var existing = await operations.FindByNameAsync(root, finalName, token);
            if (existing is not null)
            {
                if (existing.OwnerId != ownerId) throw new GenerationStoppedException("artifact_ownership_conflict");
                return existing;
            }
            // The ordinary admission checks require this file to be in the existing trusted
            // namespace. This assertion also binds the authenticated owner to its current bytes.
            await LocalGenerationStorage.VerifyPublicationContentAsync(Path.Combine(root, finalName), operation, token);
            await operations.RegisterExistingWithinAdmissionAsync(operation, token);
            return operation;
        }, ct);
    }

    public async Task DeleteManyAsync(IReadOnlyList<ArtifactPublication> requested, CancellationToken ct)
    {
        if (requested.Count == 0) return;
        if (requested.Count > 64 || requested.Select(operation => operation.Id).Distinct().Count() != requested.Count)
            throw new GenerationStoppedException("artifact_batch_invalid");
        var ownership = new List<FileStream>();
        try
        {
            // All callers acquire stripes in the same order; do not wait for filesystem
            // ownership while holding database admission.
            foreach (var stripe in requested.GroupBy(operation => operation.Id.ToByteArray()[0] & 63).OrderBy(group => group.Key))
                ownership.Add(await AcquireAsync(stripe.First().Id, ct));
            var found = await operations.GetByIdsAsync(requested.Select(operation => operation.Id).ToArray(), ct);
            var current = found.Where(operation => operation.Phase is not
                (ArtifactPublicationPhase.Deleted or ArtifactPublicationPhase.Abandoned)).ToArray();
            if (current.Length == 0) return;
            foreach (var operation in current)
            {
                storage.ValidatePublicationPaths(operation);
                if (operation.Phase is not (ArtifactPublicationPhase.Published or ArtifactPublicationPhase.Deleting))
                    throw new GenerationStoppedException("artifact_deletion_not_ready");
            }
            var deleting = await coordinator.ReclaimAsync(token => operations.PrepareDeletionBatchAsync(current.Select(operation => operation.Id).ToArray(), token), ct);
            await using (var publication = await storage.AcquirePublicationNamespaceAsync(ct))
            {
                foreach (var operation in deleting)
                {
                    string final = Path.Combine(operation.Root, operation.FinalName);
                    if (Path.Exists(final)) await LocalGenerationStorage.VerifyPublicationContentAsync(final, operation, ct);
                }
                foreach (var group in deleting.GroupBy(operation => operation.Root, StringComparer.Ordinal))
                    DurableArtifactFile.DeleteMany(group.Key, group.Select(operation => operation.FinalName).ToArray());
            }
            await coordinator.RemoveArtifactsAsync(deleting, async token =>
            {
                await operations.DeleteDeletingBatchWithinAdmissionAsync(deleting, token);
                return true;
            }, ct);
        }
        finally { foreach (var file in ownership.AsEnumerable().Reverse()) await file.DisposeAsync(); }
    }

    public async Task DeleteAsync(ArtifactPublication operation, CancellationToken ct)
    {
        using var measurement = LinerNotes.Application.Common.Diagnostics.DiscoveryDiagnostics.Measure("storage.artifact_delete");
        await using var ownership = await AcquireAsync(operation.Id, ct);
        await DeleteOwnedAsync(operation, ct);
    }

    private async Task DeleteOwnedAsync(ArtifactPublication operation, CancellationToken ct)
    {
        var current = await operations.FindAsync(operation.Id, ct);
        if (current is null) return;
        operation = current;
        storage.ValidatePublicationPaths(operation);
        if (operation.Phase is ArtifactPublicationPhase.Deleted or ArtifactPublicationPhase.Abandoned) return;
        if (operation.Phase is ArtifactPublicationPhase.Reserved or ArtifactPublicationPhase.Prepared or
            ArtifactPublicationPhase.Publishing or ArtifactPublicationPhase.Canceling)
        {
            if (operation.Phase is ArtifactPublicationPhase.Prepared or ArtifactPublicationPhase.Publishing)
                operation = await coordinator.ReclaimAsync(token => operations.TransitionAsync(operation.Id, operation.Version,
                    operation.Phase, ArtifactPublicationPhase.Canceling, token), ct);
            await using (var publication = await storage.AcquirePublicationNamespaceAsync(ct))
            {
                string staging = Path.Combine(operation.Root, operation.StageName), finalName = Path.Combine(operation.Root, operation.FinalName);
                if (operation.Phase == ArtifactPublicationPhase.Reserved)
                {
                    if (Path.Exists(finalName)) throw new GenerationStoppedException("artifact_unexpected_final");
                    if (Path.Exists(staging))
                    {
                        using var candidate = DurableArtifactFile.OpenRead(staging);
                        if (candidate.Length > operation.ReservedBytes) throw new GenerationStoppedException("artifact_staging_invalid");
                    }
                }
                else
                {
                    if (Path.Exists(staging)) await LocalGenerationStorage.VerifyPublicationContentAsync(staging, operation, ct);
                    if (Path.Exists(finalName)) await LocalGenerationStorage.VerifyPublicationContentAsync(finalName, operation, ct);
                    DurableArtifactFile.Delete(operation.Root, operation.FinalName);
                }
                DurableArtifactFile.Delete(operation.Root, operation.StageName);
            }
            await coordinator.ReclaimAsync(token => operations.TransitionAsync(operation.Id, operation.Version,
                operation.Phase, ArtifactPublicationPhase.Abandoned, token), ct);
            return;
        }
        if (operation.Phase == ArtifactPublicationPhase.Published)
            operation = await coordinator.ReclaimAsync(token => operations.TransitionAsync(operation.Id, operation.Version,
                ArtifactPublicationPhase.Published, ArtifactPublicationPhase.Deleting, token), ct);
        if (operation.Phase != ArtifactPublicationPhase.Deleting) throw new GenerationStoppedException("artifact_deletion_not_ready");
        string final = Path.Combine(operation.Root, operation.FinalName);
        await using (var publication = await storage.AcquirePublicationNamespaceAsync(ct))
        {
            if (Path.Exists(final)) await LocalGenerationStorage.VerifyPublicationContentAsync(final, operation, ct);
            DurableArtifactFile.Delete(operation.Root, operation.FinalName);
        }
        await coordinator.RemoveArtifactAsync(operation, token => operations.TransitionAsync(operation.Id, operation.Version,
            ArtifactPublicationPhase.Deleting, ArtifactPublicationPhase.Deleted, token), ct);
    }

    public async Task RecoverAsync(CancellationToken ct, Guid? operationId = null)
    {
        using var measurement = LinerNotes.Application.Common.Diagnostics.DiscoveryDiagnostics.Measure("storage.artifact_recovery");
        var elapsed = Stopwatch.StartNew();
        var pending = await operations.ReadActiveAsync(ct);
        if (pending.Count > 64) throw new GenerationStoppedException("artifact_queue_invalid");
        foreach (var operation in pending.Where(operation => operationId is null || operation.Id == operationId))
        {
            ct.ThrowIfCancellationRequested();
            storage.ValidatePublicationPaths(operation);
            if (operation.Phase is not (ArtifactPublicationPhase.Reserved or ArtifactPublicationPhase.Deleting or ArtifactPublicationPhase.Canceling)) continue;
            await using var ownership = TryAcquire(operation.Id);
            if (ownership is null)
            {
                if (operationId is not null) throw new GenerationStoppedException("artifact_publication_busy");
                continue; // Another live host owns this operation; never take over its open file.
            }
            await DeleteOwnedAsync(operation, ct);
            if (operationId is null && elapsed.Elapsed > TimeSpan.FromSeconds(15)) return;
        }
        if (operationId is not null || accountOwnership is null) return;
        Guid? cursor;
        lock (RecoveryCursorLock) cursor = recoveryCursor;
        var owners = await operations.GetOrphanedOwnersAsync(cursor, 4, ct);
        if (owners.Count == 0 && cursor is not null) owners = await operations.GetOrphanedOwnersAsync(null, 4, ct);
        foreach (var owner in owners)
        {
            await using var account = await accountOwnership.AcquireAsync(owner, ct);
            if (!await operations.OwnerExistsAsync(owner, ct))
            {
                var page = await operations.GetOwnerCleanupPageAsync(owner, 16, ct);
                foreach (var group in page.Where(operation => operation.Phase is ArtifactPublicationPhase.Published or ArtifactPublicationPhase.Deleting).Chunk(64))
                    await DeleteManyAsync(group, ct);
                foreach (var operation in page.Where(operation => operation.Phase is not (ArtifactPublicationPhase.Published or ArtifactPublicationPhase.Deleting)))
                    await DeleteAsync(operation, ct);
                if ((await operations.GetOwnerCleanupPageAsync(owner, 1, ct)).Count == 0)
                    await coordinator.ReclaimAsync(async token => { await operations.DeleteTerminalOwnerRowsAsync(owner, token); return true; }, ct);
            }
            lock (RecoveryCursorLock) recoveryCursor = owner;
            if (elapsed.Elapsed > TimeSpan.FromSeconds(15)) return;
        }
    }

    private FileStream? TryAcquire(Guid id)
    {
        try { return DurableArtifactFile.OpenOwnership(OwnershipPath(id)); }
        catch (IOException error) when (error.Message == "artifact_ownership_busy") { return null; }
    }
    private string OwnershipPath(Guid id) => options.LeasePath + ".artifact-" + (id.ToByteArray()[0] & 63).ToString("D2");

    private async Task<FileStream> AcquireAsync(Guid id, CancellationToken ct)
    {
        // A fixed set of lock inodes bounds metadata and avoids unlink/recreate split ownership.
        string path = OwnershipPath(id);
        for (int attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return DurableArtifactFile.OpenOwnership(path);
            }
            catch (IOException error) when (error.Message == "artifact_ownership_busy" && attempt < 100) { await Task.Delay(100, ct); }
        }
    }
}
