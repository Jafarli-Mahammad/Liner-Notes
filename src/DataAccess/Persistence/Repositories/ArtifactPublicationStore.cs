using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.Persistence.Accounting;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class ArtifactPublicationStore(DataContext db) : IArtifactPublicationStore
{
    private IQueryable<ArtifactPublicationRecord> Records => db.Set<ArtifactPublicationRecord>().AsNoTracking();
    public async Task<IReadOnlyList<ArtifactPublication>> ReadActiveAsync(CancellationToken ct) =>
        (await Records.Where(row => row.Phase != ArtifactPublicationPhase.Published && row.Phase != ArtifactPublicationPhase.Abandoned && row.Phase != ArtifactPublicationPhase.Deleted)
            .OrderBy(row => row.Id).Take(65).ToArrayAsync(ct)).Select(Map).ToArray();

    public async Task<ArtifactPublication?> FindAsync(Guid id, CancellationToken ct) =>
        await Records.SingleOrDefaultAsync(row => row.Id == id, ct) is { } row ? Map(row) : null;

    public async Task<ArtifactPublication?> FindByNameAsync(string root, string finalName, CancellationToken ct) =>
        await Records.SingleOrDefaultAsync(row => row.Root == root && row.FinalName == finalName && row.Phase != ArtifactPublicationPhase.Abandoned && row.Phase != ArtifactPublicationPhase.Deleted, ct)
            is { } row ? Map(row) : null;

    public async Task<IReadOnlyList<ArtifactPublication>> GetOwnerPageAsync(Guid ownerId, Guid? after, int limit, CancellationToken ct)
    {
        if (ownerId == Guid.Empty || limit is < 1 or > 100) throw new ArgumentException("Bounded owner page required.");
        return (await Records.Where(row => row.OwnerId == ownerId && (after == null || row.Id.CompareTo(after.Value) > 0))
            .OrderBy(row => row.Id).Take(limit).ToArrayAsync(ct)).Select(Map).ToArray();
    }

    public async Task RegisterExistingWithinAdmissionAsync(ArtifactPublication operation, CancellationToken ct)
    {
        RequireTransaction();
        if (operation.Id == Guid.Empty || operation.OwnerId is null || operation.OwnerId == Guid.Empty ||
            operation.Phase != ArtifactPublicationPhase.Published || operation.Version != 3 || operation.LastWriteTimeUtcTicks <= 0 ||
            operation.ContentBytes is < 1 or > 67_108_864 || operation.ReservedBytes != operation.ContentBytes ||
            !SafeName(operation.FinalName) || !SafeName(operation.StageName) || operation.Sha256.Length != 64 ||
            operation.Sha256.Any(c => !char.IsAsciiHexDigit(c)))
            throw new GenerationStoppedException("artifact_adoption_invalid");
        db.Add(new ArtifactPublicationRecord
        {
            Id = operation.Id, OwnerId = operation.OwnerId, Category = operation.Category, Root = operation.Root,
            FinalName = operation.FinalName, StageName = operation.StageName, ReservedBytes = operation.ReservedBytes,
            ContentBytes = operation.ContentBytes, Sha256 = operation.Sha256, Phase = operation.Phase,
            Version = operation.Version, LastWriteTimeUtcTicks = operation.LastWriteTimeUtcTicks
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task ReserveAsync(ArtifactPublication operation, CancellationToken ct)
    {
        RequireTransaction();
        if (operation.OwnerId == Guid.Empty || operation.Category == "copies" && operation.OwnerId is null ||
            operation.StageName != operation.Id.ToString("N") + ".staged" || operation.LastWriteTimeUtcTicks != 0 ||
            operation.Id == Guid.Empty || operation.Version != 0 || operation.Phase != ArtifactPublicationPhase.Reserved ||
            operation.ReservedBytes is < 1 or > 64 * 1024 * 1024 || operation.ContentBytes != operation.ReservedBytes ||
            operation.Sha256.Length != 64 || operation.Sha256.Any(c => !char.IsAsciiHexDigit(c)) ||
            !Path.IsPathFullyQualified(operation.Root) || operation.Root.Length > 1024 ||
            !SafeName(operation.FinalName) || !SafeName(operation.StageName) || operation.FinalName == operation.StageName)
            throw new GenerationStoppedException("artifact_reservation_invalid");
        if (await Records.CountAsync(row => row.Phase != ArtifactPublicationPhase.Published && row.Phase != ArtifactPublicationPhase.Abandoned && row.Phase != ArtifactPublicationPhase.Deleted, ct) >= 64)
            throw new GenerationStoppedException("artifact_queue_full");
        db.Add(new ArtifactPublicationRecord
        {
            Id = operation.Id, Category = operation.Category, Root = operation.Root, FinalName = operation.FinalName,
            StageName = operation.StageName, ReservedBytes = operation.ReservedBytes, ContentBytes = operation.ContentBytes,
            Sha256 = operation.Sha256.ToLowerInvariant(), Phase = operation.Phase, Version = 0, OwnerId = operation.OwnerId
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<ArtifactPublication> TransitionAsync(Guid id, long expectedVersion, ArtifactPublicationPhase expectedPhase,
        ArtifactPublicationPhase nextPhase, CancellationToken ct, long? verifiedWriteTimeUtcTicks = null)
    {
        RequireTransaction();
        bool legal = (expectedPhase, nextPhase) is
            (ArtifactPublicationPhase.Reserved, ArtifactPublicationPhase.Prepared) or
            (ArtifactPublicationPhase.Prepared, ArtifactPublicationPhase.Publishing) or
            (ArtifactPublicationPhase.Publishing, ArtifactPublicationPhase.Published) or
            (ArtifactPublicationPhase.Reserved, ArtifactPublicationPhase.Abandoned) or
            (ArtifactPublicationPhase.Published, ArtifactPublicationPhase.Deleting) or
            (ArtifactPublicationPhase.Deleting, ArtifactPublicationPhase.Deleted) or
            (ArtifactPublicationPhase.Prepared, ArtifactPublicationPhase.Canceling) or
            (ArtifactPublicationPhase.Publishing, ArtifactPublicationPhase.Canceling) or
            (ArtifactPublicationPhase.Canceling, ArtifactPublicationPhase.Abandoned);
        if (expectedPhase == ArtifactPublicationPhase.Reserved && nextPhase == ArtifactPublicationPhase.Prepared)
        {
            if (verifiedWriteTimeUtcTicks is not > 0) throw new GenerationStoppedException("artifact_preparation_invalid");
        }
        else if (verifiedWriteTimeUtcTicks is not null) throw new GenerationStoppedException("artifact_transition_invalid");
        if (!legal) throw new GenerationStoppedException("artifact_transition_invalid");
        int count = await db.Set<ArtifactPublicationRecord>().Where(row => row.Id == id && row.Version == expectedVersion && row.Phase == expectedPhase)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Phase, nextPhase).SetProperty(row => row.Version, expectedVersion + 1)
                .SetProperty(row => row.LastWriteTimeUtcTicks, row => verifiedWriteTimeUtcTicks ?? row.LastWriteTimeUtcTicks), ct);
        if (count != 1) throw new GenerationStoppedException("artifact_publication_fenced");
        return (await FindAsync(id, ct))!;
    }

    public async Task DeleteTerminalOwnerRowsAsync(Guid ownerId, CancellationToken ct)
    {
        RequireTransaction();
        if (await Records.AnyAsync(row => row.OwnerId == ownerId && row.Phase != ArtifactPublicationPhase.Abandoned && row.Phase != ArtifactPublicationPhase.Deleted, ct))
            throw new GenerationStoppedException("artifact_owner_cleanup_incomplete");
        await db.Set<ArtifactPublicationRecord>().Where(row => row.OwnerId == ownerId).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<ArtifactPublication>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count is < 1 or > 64 || ids.Distinct().Count() != ids.Count) throw new GenerationStoppedException("artifact_batch_invalid");
        return (await Records.Where(row => ids.Contains(row.Id)).OrderBy(row => row.Id).ToArrayAsync(ct)).Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<ArtifactPublication>> PrepareDeletionBatchAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        RequireTransaction();
        var rows = await GetByIdsAsync(ids, ct);
        if (rows.Count != ids.Count || rows.Any(row => (row.Phase, row.Version) is not
            ((ArtifactPublicationPhase.Published, 3) or (ArtifactPublicationPhase.Deleting, 4))))
            throw new GenerationStoppedException("artifact_deletion_fenced");
        var published = rows.Where(row => row.Phase == ArtifactPublicationPhase.Published).Select(row => row.Id).ToArray();
        int changed = await db.Set<ArtifactPublicationRecord>().Where(row => published.Contains(row.Id) && row.Phase == ArtifactPublicationPhase.Published && row.Version == 3)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Phase, ArtifactPublicationPhase.Deleting).SetProperty(row => row.Version, 4), ct);
        if (changed != published.Length) throw new GenerationStoppedException("artifact_deletion_fenced");
        return rows.Select(row => row with { Phase = ArtifactPublicationPhase.Deleting, Version = 4 }).ToArray();
    }

    public async Task DeleteDeletingBatchWithinAdmissionAsync(IReadOnlyList<ArtifactPublication> operations, CancellationToken ct)
    {
        RequireTransaction();
        var ids = operations.Select(operation => operation.Id).ToArray();
        if (ids.Length is < 1 or > 64 || ids.Distinct().Count() != ids.Length ||
            operations.Any(operation => operation.Phase != ArtifactPublicationPhase.Deleting || operation.Version != 4))
            throw new GenerationStoppedException("artifact_batch_invalid");
        int changed = await db.Set<ArtifactPublicationRecord>().Where(row => ids.Contains(row.Id) && row.Phase == ArtifactPublicationPhase.Deleting && row.Version == 4)
            .ExecuteDeleteAsync(ct);
        if (changed != ids.Length) throw new GenerationStoppedException("artifact_deletion_fenced");
    }

    public Task<bool> OwnerExistsAsync(Guid ownerId, CancellationToken ct) => db.Users.AnyAsync(user => user.Id == ownerId, ct);

    public async Task<IReadOnlyList<Guid>> GetOrphanedOwnersAsync(Guid? after, int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 4) throw new ArgumentException("Bounded orphan page required.");
        return await Records.Where(row => row.OwnerId != null && (after == null || row.OwnerId.Value.CompareTo(after.Value) > 0) &&
                !db.Users.Any(user => user.Id == row.OwnerId.Value))
            .Select(row => row.OwnerId!.Value).Distinct().Order().Take(limit).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<ArtifactPublication>> GetOwnerCleanupPageAsync(Guid ownerId, int limit, CancellationToken ct)
    {
        if (ownerId == Guid.Empty || limit is < 1 or > 64) throw new ArgumentException("Bounded owner cleanup page required.");
        return (await Records.Where(row => row.OwnerId == ownerId && row.Phase != ArtifactPublicationPhase.Abandoned && row.Phase != ArtifactPublicationPhase.Deleted)
            .OrderBy(row => row.Id).Take(limit).ToArrayAsync(ct)).Select(Map).ToArray();
    }

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new GenerationStoppedException("artifact_write_requires_admission");
    }
    private static bool SafeName(string name) => name.Length is > 0 and <= 128 && name is not "." and not ".." &&
        name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
    private static ArtifactPublication Map(ArtifactPublicationRecord row) =>
        new(row.Id, row.Category, row.Root, row.FinalName, row.StageName, row.ReservedBytes, row.ContentBytes, row.Sha256, row.Phase, row.Version, row.OwnerId, row.LastWriteTimeUtcTicks);
}
