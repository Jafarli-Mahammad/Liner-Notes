using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
namespace LinerNotes.Infrastructure.Storage;

public interface IStorageFreeSpaceProbe { long AvailableBytes(string path); }
public sealed class DriveStorageFreeSpaceProbe : IStorageFreeSpaceProbe
{
    public long AvailableBytes(string path) => new DriveInfo(path).AvailableFreeSpace;
}
public sealed class CoordinatedStorageOptions
{
    public long MinimumFreeBytes { get; set; } = 512 * 1024 * 1024;
    public string[] FilesystemPaths { get; set; } = [];
    public void Validate()
    {
        if (MinimumFreeBytes is < 67_108_864 or > 10_737_418_240 || FilesystemPaths.Length is < 1 or > 16 ||
            FilesystemPaths.Any(p => !Path.IsPathFullyQualified(p) || !Directory.Exists(p)))
            throw new GenerationStoppedException("discovery_filesystem_inventory_unconfigured");
    }
    public void RequireHeadroom(long reservation, IStorageFreeSpaceProbe? probe = null)
    {
        Validate();
        try
        {
            foreach (var path in FilesystemPaths)
                if ((probe ?? new DriveStorageFreeSpaceProbe()).AvailableBytes(path) < checked(MinimumFreeBytes + reservation))
                    throw new GenerationStoppedException("filesystem_headroom");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OverflowException)
        { throw new GenerationStoppedException("filesystem_inventory_unknown"); }
    }
}

public sealed partial class LocalGenerationStorage
{
    public async Task<IStorageReclamationLease> PrepareReclamationAsync(CancellationToken ct)
    {
        using var measurement = LinerNotes.Application.Common.Diagnostics.DiscoveryDiagnostics.Measure("storage.preflight");
        if (coordinated is null) throw new GenerationStoppedException("discovery_filesystem_inventory_unconfigured");
        coordinated.RequireHeadroom(0, filesystemProbe);
        var admission = await (admissionGate ?? StorageAdmissionGate.Fallback).AcquireAsync(ct);
        FileStream? file = null;
        var verification = new ArtifactVerificationSession(options.EnableKernelReadLeases);
        try
        {
            file = OpenLease(shared: true);
            var inventory = await ReadTrustedInventoryAsync(ct);
            var now = clock.GetUtcNow();
            if (inventory.MeasuredAtUtc > now) throw new GenerationStoppedException("stale_or_unknown_storage_inventory");
            inventory = inventory with { MeasuredAtUtc = now };
            await ValidateArtifactsAsync(inventory, ct, verification);
            return new ReclamationLease(this, file, inventory, verification, admission);
        }
        catch { verification.Dispose(); if (file is not null) await file.DisposeAsync(); admission.Dispose(); throw; }
    }

    public async Task<IStorageReclamationLease> AcquireReclamationAsync(GenerationStorageState database, CancellationToken ct)
    {
        if (coordinated is null) throw new GenerationStoppedException("discovery_filesystem_inventory_unconfigured");
        coordinated.RequireHeadroom(0, filesystemProbe);
        var file = OpenLease();
        var verification = new ArtifactVerificationSession(options.EnableKernelReadLeases);
        try
        {
            var inventory = await ReadTrustedInventoryAsync(ct);
            var now = clock.GetUtcNow();
            if (inventory.MeasuredAtUtc > now) throw new GenerationStoppedException("stale_or_unknown_storage_inventory");
            inventory = inventory with { MeasuredAtUtc = now };
            CheckInventory(inventory);
            if (database.DatabaseBytes < 0 || !database.Revision.StartsWith("managed-v1:", StringComparison.Ordinal) ||
                inventory.DatabaseRevision != database.Revision)
                throw new GenerationStoppedException("database_inventory_changed");
            await ValidateArtifactsAsync(inventory, ct, verification);
            return new ReclamationLease(this, file, inventory, verification);
        }
        catch { verification.Dispose(); await file.DisposeAsync(); throw; }
    }

    private sealed class ReclamationLease(LocalGenerationStorage owner, FileStream file, GenerationInventory inventory,
        ArtifactVerificationSession verification, IDisposable? admission = null)
        : IStorageReclamationLease
    {
        public bool UsesCommitWitness => owner.commits is not null;
        public async Task RebaseAsync(GenerationStorageState database, CancellationToken ct)
        {
            var current = await owner.ReadTrustedInventoryAsync(ct);
            var now = owner.UtcNow;
            if (current.MeasuredAtUtc > now) throw new GenerationStoppedException("stale_or_unknown_storage_inventory");
            current = current with { MeasuredAtUtc = now };
            owner.CheckInventory(current);
            if (current.DatabaseRevision != database.Revision || current.Categories.Count != inventory.Categories.Count ||
                Categories.Any(category => current.Categories[category] != inventory.Categories[category]))
                throw new GenerationStoppedException("database_inventory_changed");
            inventory = current with { DatabaseBytes = database.DatabaseBytes, ReservedArtifactBytes = database.ReservedArtifactBytes };
            if (owner.commits is not null)
            {
                GenerationInventory? published = null;
                try { published = await owner.ReadInventoryAsync(ct); } catch (GenerationStoppedException) { }
                if (published is null || published.CommitId != inventory.CommitId || published.DatabaseRevision != inventory.DatabaseRevision)
                {
                    await owner.ValidateArtifactsAsync(inventory, ct, verification);
                    await owner.WriteKnownInventoryAsync(inventory, ct);
                }
            }
        }

        private ManagedStorageCommit? committedWitness;
        private StoragePublicationPlan? artifactPlan;
        public StoragePublicationPlan CreatePublicationPlan() => artifactPlan ?? owner.PublicationPlan(inventory);
        public async Task ApplyDeletedArtifactsAsync(IReadOnlyList<ArtifactPublication> operations, CancellationToken ct)
        {
            if (owner.publications is null || operations.Count is < 1 or > 64 ||
                operations.Select(operation => operation.Id).Distinct().Count() != operations.Count)
                throw new GenerationStoppedException("artifact_batch_invalid");
            if ((await owner.publications.GetByIdsAsync(operations.Select(operation => operation.Id).ToArray(), ct)).Count != 0)
                throw new GenerationStoppedException("artifact_deletion_fenced");
            foreach (var operation in operations)
            {
                owner.ValidatePublicationPaths(operation);
                if (operation.Phase != ArtifactPublicationPhase.Deleting || operation.Version != 4 ||
                    Path.Exists(Path.Combine(operation.Root, operation.FinalName)) || Path.Exists(Path.Combine(operation.Root, operation.StageName)))
                    throw new GenerationStoppedException("artifact_deletion_fenced");
            }
            var before = inventory;
            var expected = await owner.MeasureArtifactsAsync(ct, null, verification, operations);
            if (Categories.Any(category => expected[category] != before.Categories[category]))
                throw new GenerationStoppedException("artifact_inventory_changed");
            inventory = inventory with { Categories = await owner.MeasureArtifactsAsync(ct, null, verification) };
            artifactPlan = owner.PublicationPlan(inventory) with
            {
                BeforeInventoryJson = System.Text.Json.JsonSerializer.Serialize(before),
                OwnedChangesJson = System.Text.Json.JsonSerializer.Serialize(operations.Select(operation => new { operation.Id }))
            };
        }
        public async Task ApplyDeletedArtifactAsync(ArtifactPublication operation, CancellationToken ct)
        {
            if (owner.publications is null) throw new GenerationStoppedException("artifact_publication_missing");
            owner.ValidatePublicationPaths(operation);
            var removed = await owner.publications.FindAsync(operation.Id, ct);
            if (operation.Phase != ArtifactPublicationPhase.Deleting ||
                removed != (operation with { Phase = ArtifactPublicationPhase.Deleted, Version = operation.Version + 1 }) ||
                Path.Exists(Path.Combine(operation.Root, operation.FinalName)) || Path.Exists(Path.Combine(operation.Root, operation.StageName)))
                throw new GenerationStoppedException("artifact_deletion_fenced");
            // Only a durable deletion intent authorizes absence of this exact known file.
            // Its previous digest contribution remains charged until this transaction finalizes.
            var before = inventory;
            var expected = await owner.MeasureArtifactsAsync(ct, null, verification, [operation]);
            if (Categories.Any(category => expected[category] != before.Categories[category]))
                throw new GenerationStoppedException("artifact_inventory_changed");
            inventory = inventory with { Categories = await owner.MeasureArtifactsAsync(ct, null, verification) };
            artifactPlan = owner.PublicationPlan(inventory) with
            {
                BeforeInventoryJson = System.Text.Json.JsonSerializer.Serialize(before),
                OwnedChangesJson = System.Text.Json.JsonSerializer.Serialize(new[] { new { operation.Id } })
            };
        }
        public void BindCommit(ManagedStorageCommit commit) => committedWitness = commit;
        public async Task VerifyAsync(GenerationStorageState database, CancellationToken ct)
        {
            if (database.DatabaseBytes < 0) throw new GenerationStoppedException("database_inventory_changed");
            owner.coordinated!.RequireHeadroom(0, owner.filesystemProbe);
            await owner.ValidateArtifactsAsync(inventory, ct, verification);
        }
        public Task PublishCommittedInventoryAsync(GenerationStorageState committed, CancellationToken ct)
        {
            if (committedWitness is not null)
            {
                if (committed.Revision != committedWitness.ExpectedRevision) throw new GenerationStoppedException("storage_commit_witness_mismatch");
                inventory = inventory with { CommitId = committedWitness.CommitId };
            }
            return owner.PublishCommittedAsync(inventory, committed, ct, allowAtCutoff: true, verification: verification);
        }
        public async ValueTask DisposeAsync()
        {
            verification.Dispose();
            try { await file.DisposeAsync(); }
            finally { admission?.Dispose(); }
        }
    }
    public async Task RefreshKnownInventoryAsync(GenerationStorageState database, CancellationToken ct)
    {
        if (coordinated is null) throw new GenerationStoppedException("discovery_filesystem_inventory_unconfigured");
        await using var lease = OpenLease();
        var previous = await ReadTrustedInventoryAsync(ct);
        var now = clock.GetUtcNow();
        if (previous.MeasuredAtUtc > now) throw new GenerationStoppedException("stale_or_unknown_storage_inventory");
        previous = previous with { MeasuredAtUtc = now };
        CheckInventory(previous);
        if (database.DatabaseBytes < 0 || !database.Revision.StartsWith("managed-v1:", StringComparison.Ordinal) ||
            previous.DatabaseRevision != database.Revision)
            throw new GenerationStoppedException("database_inventory_changed");
        await PublishCommittedAsync(previous, database with
        {
            DatabaseBytes = Math.Max(previous.DatabaseBytes, database.DatabaseBytes)
        }, ct, allowAtCutoff: true);
    }
    // Set by DI through a separate constructor, keeping legacy direct construction source-compatible.
    private readonly CoordinatedStorageOptions? coordinated;
    private readonly IStorageFreeSpaceProbe filesystemProbe = new DriveStorageFreeSpaceProbe();
    private readonly StorageAdmissionGate? admissionGate;
    private readonly IManagedStorageCommitStore? commits;
    private readonly IArtifactPublicationStore? publications;
    public LocalGenerationStorage(GenerationStorageOptions options, TimeProvider clock, CoordinatedStorageOptions coordinatedOptions,
        IStorageFreeSpaceProbe? probe = null, StorageAdmissionGate? admissionGate = null, IManagedStorageCommitStore? commits = null, IArtifactPublicationStore? publications = null,
        LinerNotes.Application.Features.Discovery.DiscoveryRuntimeOptions? runtime = null)
        : this(options, clock)
    {
        coordinated = coordinatedOptions; filesystemProbe = probe ?? new DriveStorageFreeSpaceProbe(); this.admissionGate = admissionGate;
        this.commits = runtime is not null && !runtime.ManagedWritesEnabled ? null : commits;
        this.publications = this.commits is null ? null : publications;
    }

    public async Task<ICoordinatedStorageLease> AcquireCoordinatedAsync(GenerationStorageState database, CancellationToken ct)
    {
        if (coordinated is null) throw new GenerationStoppedException("discovery_filesystem_inventory_unconfigured");
        coordinated.RequireHeadroom(0, filesystemProbe);
        var file = OpenLease();
        var verification = new ArtifactVerificationSession(options.EnableKernelReadLeases);
        try
        {
            var inventory = await ReadTrustedInventoryAsync(ct);
            CheckInventory(inventory);
            if (!database.Revision.StartsWith("managed-v1:", StringComparison.Ordinal) || inventory.DatabaseRevision != database.Revision)
                throw new GenerationStoppedException("database_inventory_changed");
            await ValidateArtifactsAsync(inventory, ct, verification);
            // Ledger/schema unchanged: charge current observed relation size, including background page growth.
            // Never silently accept an unaccounted row mutation or a changed schema/trigger inventory.
            inventory = inventory with { DatabaseBytes = database.DatabaseBytes, ReservedArtifactBytes = database.ReservedArtifactBytes };
            if (Total(inventory) >= StopBytes) throw new GenerationStoppedException("storage_headroom");
            return new Lease(this, file, inventory, coordinatedMode: true, verification: verification);
        }
        catch { verification.Dispose(); await file.DisposeAsync(); throw; }
    }

    public async Task<ICoordinatedStorageLease> PrepareCoordinatedAsync(CancellationToken ct)
    {
        using var measurement = LinerNotes.Application.Common.Diagnostics.DiscoveryDiagnostics.Measure("storage.preflight");
        if (coordinated is null) throw new GenerationStoppedException("discovery_filesystem_inventory_unconfigured");
        coordinated.RequireHeadroom(0, filesystemProbe);
        var admission = await (admissionGate ?? StorageAdmissionGate.Fallback).AcquireAsync(ct);
        FileStream? file = null;
        var verification = new ArtifactVerificationSession(options.EnableKernelReadLeases);
        try
        {
            file = OpenLease(shared: true);
            var inventory = await ReadTrustedInventoryAsync(ct);
            CheckInventory(inventory);
            await ValidateArtifactsAsync(inventory, ct, verification);
            return new Lease(this, file, inventory, coordinatedMode: true, verification: verification, admission: admission);
        }
        catch { verification.Dispose(); if (file is not null) await file.DisposeAsync(); admission.Dispose(); throw; }
    }
    private async Task PublishCommittedAsync(GenerationInventory previous, GenerationStorageState committed, CancellationToken ct, bool allowAtCutoff = false,
        ArtifactVerificationSession? verification = null)
    {
        if (coordinated is null) throw new GenerationStoppedException("discovery_filesystem_inventory_unconfigured");
        coordinated.RequireHeadroom(0, filesystemProbe);
        await ValidateArtifactsAsync(previous, ct, verification);
        var current = previous with { DatabaseBytes = committed.DatabaseBytes, ReservedArtifactBytes = committed.ReservedArtifactBytes, DatabaseRevision = committed.Revision, MeasuredAtUtc = clock.GetUtcNow() };
        if (!allowAtCutoff && Total(current) >= StopBytes) throw new GenerationStoppedException("storage_headroom");
        await WriteKnownInventoryAsync(current, ct);
    }
}
