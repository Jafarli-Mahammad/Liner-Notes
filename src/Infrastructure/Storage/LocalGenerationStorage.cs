using System.Security.Cryptography;
using LinerNotes.Application.Common.Interfaces.Discovery;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Interfaces;

namespace LinerNotes.Infrastructure.Storage;

public sealed class GenerationStorageOptions
{
    public string? InventoryPath { get; set; }
    public string? LeasePath { get; set; }
    public long? DatabaseOverheadBytesPerPick { get; set; }
    public bool EnableKernelReadLeases { get; set; } = true;
    public Dictionary<string, string[]> ArtifactRoots { get; set; } = new(StringComparer.Ordinal);
}

public sealed record ArtifactMeasurement(long Bytes, string Revision);
public sealed record GenerationInventory(DateTimeOffset MeasuredAtUtc, bool Reconciled,
    long DatabaseBytes, string DatabaseRevision, Dictionary<string, ArtifactMeasurement> Categories, Guid? CommitId = null, long ReservedArtifactBytes = 0);

/// <summary>Explicit local roots and reconciled inventory under one cross-process batch lease.</summary>
public sealed partial class LocalGenerationStorage(GenerationStorageOptions options, TimeProvider clock) : IGenerationStorage, ILocalEmailStorage, ICoordinatedGenerationStorage
{
    private DateTimeOffset UtcNow => clock.GetUtcNow();
    private long OverheadBytesPerPick => options.DatabaseOverheadBytesPerPick!.Value;
    public const long StopBytes = 1_000_000_000;
    public static IReadOnlyList<string> Categories { get; } = ["recordings", "cache", "reports", "copies", "backups", "partials"];

    public async Task<IGenerationStorageLease> AcquireAsync(GenerationStorageState database, CancellationToken cancellationToken = default)
    {
        var lease = OpenLease();
        var verification = new ArtifactVerificationSession(options.EnableKernelReadLeases);
        try
        {
            var inventory = await ReadTrustedInventoryAsync(cancellationToken).ConfigureAwait(false);
            await ValidateAsync(inventory, database, cancellationToken, verification: verification).ConfigureAwait(false);
            return new Lease(this, lease, inventory, verification: verification);
        }
        catch { verification.Dispose(); await lease.DisposeAsync().ConfigureAwait(false); throw; }
    }

    public async Task<ILocalEmailWriteLease> AcquireEmailAsync(GenerationStorageState database, long bytes,
        IReadOnlyList<string> ownedPaths, bool recovery, CancellationToken ct)
    {
        var file = OpenLease();
        var verification = new ArtifactVerificationSession(options.EnableKernelReadLeases);
        try
        {
            if (recovery) return new EmailLease(this, file, null, null, null, 0, verification);
            var inventory = await ReadTrustedInventoryAsync(ct).ConfigureAwait(false);
            await ValidateAsync(inventory, database, ct, verification: verification).ConfigureAwait(false);
            if (bytes <= 0 || ownedPaths.Count != 4 || ownedPaths.Distinct(StringComparer.Ordinal).Count() != 4 || ownedPaths.Any(File.Exists))
                throw new GenerationStoppedException("email_reservation_invalid");
            long allowance = checked(bytes * 3); // Three attempted partials; rename adds no bytes.
            if (checked(Total(inventory) + allowance) >= StopBytes) throw new GenerationStoppedException("storage_headroom");
            var excluded = ownedPaths.ToHashSet(StringComparer.Ordinal);
            var baseline = await MeasureArtifactsAsync(ct, excluded, verification).ConfigureAwait(false);
            return new EmailLease(this, file, inventory, baseline, excluded, allowance, verification);
        }
        catch { verification.Dispose(); await file.DisposeAsync(); throw; }
    }

    // Explicit operator helper, never called automatically by generation.
    public async Task<GenerationInventory> ReconcileAsync(GenerationStorageState database, CancellationToken cancellationToken = default)
    {
        await using var lease = OpenLease();
        var measurements = await MeasureAsync(cancellationToken).ConfigureAwait(false);
        var inventory = new GenerationInventory(clock.GetUtcNow(), true, database.DatabaseBytes, database.Revision, measurements);
        if (commits is not null)
        {
            var id = Guid.NewGuid();
            database = await commits.EstablishAsync(id, new(JsonSerializer.Serialize(inventory), JsonSerializer.Serialize(measurements),
                "[]", RootFingerprint(), clock.GetUtcNow()), cancellationToken);
            inventory = inventory with { DatabaseBytes = database.DatabaseBytes, ReservedArtifactBytes = database.ReservedArtifactBytes, DatabaseRevision = database.Revision, CommitId = id };
        }
        await ValidateAsync(inventory, database, cancellationToken).ConfigureAwait(false);
        await WriteKnownInventoryAsync(inventory, cancellationToken).ConfigureAwait(false);
        return inventory;
    }

    private FileStream OpenLease(bool shared = false)
    {
        if (options.DatabaseOverheadBytesPerPick is not > 0 || !Path.IsPathFullyQualified(options.InventoryPath ?? "") ||
            !Path.IsPathFullyQualified(options.LeasePath ?? "") || options.InventoryPath == options.LeasePath ||
            options.ArtifactRoots.Count != Categories.Count || Categories.Any(c => !options.ArtifactRoots.ContainsKey(c)))
            throw new GenerationStoppedException("storage_unconfigured");
        var roots = options.ArtifactRoots.Values.SelectMany(r => r).Select(Path.GetFullPath).ToArray();
        if (roots.Any(root => !Path.IsPathFullyQualified(root)) || roots.Distinct(StringComparer.Ordinal).Count() != roots.Length ||
            roots.Any(root => roots.Any(other => root != other && Within(root, other))) ||
            roots.Any(root => Within(options.LeasePath!, root) || Within(options.InventoryPath!, root)))
            throw new GenerationStoppedException("overlapping_storage_roots");
        foreach (var path in roots.Append(options.InventoryPath!).Append(options.LeasePath!))
            for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new GenerationStoppedException("symlink_artifact_root");
        try { return OperatingSystem.IsLinux() ? DurableArtifactFile.OpenOwnership(options.LeasePath!, shared, create: !shared) : shared
            ? new FileStream(options.LeasePath!, FileMode.Open, FileAccess.Read, FileShare.Read)
            : new FileStream(options.LeasePath!, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new GenerationStoppedException("storage_lease_unavailable"); }
        catch (UnauthorizedAccessException) { throw new GenerationStoppedException("storage_lease_unavailable"); }
    }

    private static bool LegacyRevisionEquals(string first, string second)
    {
        if (first == second) return true;
        var a = first.Split(':'); var b = second.Split(':');
        return a.Length == 6 && b.Length == 6 && a[0] == "managed-v1" && b[0] == a[0] &&
            a[1] == b[1] && a[2] == b[2] && a[5] == b[5];
    }

    private static bool Within(string file, string root) => Path.GetFullPath(file).StartsWith(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
        Path.GetFullPath(file) == Path.GetFullPath(root);

    private async Task<GenerationInventory> ReadInventoryAsync(CancellationToken ct)
    {
        try
        {
            var info = new FileInfo(options.InventoryPath!);
            if (!info.Exists || info.Length > 1_000_000) throw new GenerationStoppedException("unknown_storage_inventory");
            await using var stream = info.OpenRead();
            return await JsonSerializer.DeserializeAsync<GenerationInventory>(stream, cancellationToken: ct).ConfigureAwait(false)
                ?? throw new GenerationStoppedException("unknown_storage_inventory");
        }
        catch (IOException) { throw new GenerationStoppedException("unknown_storage_inventory"); }
        catch (JsonException) { throw new GenerationStoppedException("invalid_storage_inventory"); }
    }

    private Task WriteKnownInventoryAsync(GenerationInventory inventory, CancellationToken ct) =>
        DurableInventoryFile.WriteAsync(options.InventoryPath!, inventory, ct, commits is null ? null :
            async token => (await commits.ReadAsync(token))?.CommitId == inventory.CommitId);

    private string RootFingerprint() => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
        Categories.Select(category => new { Category = category, Roots = options.ArtifactRoots[category].Order(StringComparer.Ordinal).ToArray() }))));

    private StoragePublicationPlan PublicationPlan(GenerationInventory inventory) => new(JsonSerializer.Serialize(inventory),
        JsonSerializer.Serialize(inventory.Categories), "[]", RootFingerprint(), clock.GetUtcNow());

    private async Task<GenerationInventory> ReadTrustedInventoryAsync(CancellationToken ct)
    {
        if (commits is null) return await ReadInventoryAsync(ct).ConfigureAwait(false);
        var witness = await commits.ReadAsync(ct).ConfigureAwait(false)
            ?? throw new GenerationStoppedException("storage_commit_witness_uninitialized");
        if (witness.Plan.RootFingerprint != RootFingerprint()) throw new GenerationStoppedException("storage_roots_changed");
        Dictionary<string, ArtifactMeasurement> categories;
        try { categories = JsonSerializer.Deserialize<Dictionary<string, ArtifactMeasurement>>(witness.Plan.ArtifactCategoriesJson)
                ?? throw new GenerationStoppedException("storage_commit_artifacts_invalid"); }
        catch (JsonException) { throw new GenerationStoppedException("storage_commit_artifacts_invalid"); }
        GenerationInventory? file = null;
        try { file = await ReadInventoryAsync(ct).ConfigureAwait(false); }
        catch (GenerationStoppedException) { /* Only the trusted witness can authorize rebuilding metadata after full verification. */ }
        var now = clock.GetUtcNow();
        bool current = file is not null && file.CommitId == witness.CommitId && file.DatabaseRevision == witness.ExpectedRevision &&
            file.Categories is not null && categories.Count == file.Categories.Count && categories.All(pair => file.Categories.TryGetValue(pair.Key, out var value) && value == pair.Value) &&
            file.MeasuredAtUtc <= now && now - file.MeasuredAtUtc <= TimeSpan.FromMinutes(5);
        return current ? file! : new(now, true, 0, witness.ExpectedRevision, categories, witness.CommitId);
    }

    private void CheckInventory(GenerationInventory inventory)
    {
        var age = clock.GetUtcNow() - inventory.MeasuredAtUtc;
        if (!inventory.Reconciled || age < TimeSpan.Zero || age > TimeSpan.FromMinutes(5) ||
            inventory.Categories is null || inventory.Categories.Count != Categories.Count ||
            Categories.Any(c => !inventory.Categories.ContainsKey(c)) ||
            inventory.DatabaseBytes < 0 || string.IsNullOrWhiteSpace(inventory.DatabaseRevision) ||
            inventory.Categories.Values.Any(v => v is null || v.Bytes < 0 || string.IsNullOrWhiteSpace(v.Revision)))
            throw new GenerationStoppedException("stale_or_unknown_storage_inventory");
    }

    private async Task ValidateAsync(GenerationInventory inventory, GenerationStorageState database, CancellationToken ct, bool coordinatedMode = false,
        ArtifactVerificationSession? verification = null)
    {
        CheckInventory(inventory);
        // Physical shrink does not reduce the reconciled charge; growth still stops.
        if (database.DatabaseBytes < 0 || database.DatabaseBytes > inventory.DatabaseBytes || (!LegacyRevisionEquals(database.Revision, inventory.DatabaseRevision) || (coordinatedMode && database.Revision != inventory.DatabaseRevision)))
            throw new GenerationStoppedException("database_inventory_changed");
        await ValidateArtifactsAsync(inventory, ct, verification).ConfigureAwait(false);
        if (Total(inventory) >= StopBytes) throw new GenerationStoppedException("storage_headroom");
    }

    private async Task ValidateArtifactsAsync(GenerationInventory inventory, CancellationToken ct, ArtifactVerificationSession? verification = null)
    {
        CheckInventory(inventory);
        var actual = await MeasureArtifactsAsync(ct, null, verification).ConfigureAwait(false);
        if (Categories.Any(c => actual[c] != inventory.Categories[c])) throw new GenerationStoppedException("artifact_inventory_changed");
    }

    private static long Total(GenerationInventory inventory) => checked(inventory.DatabaseBytes + inventory.ReservedArtifactBytes + inventory.Categories.Values.Sum(v => v.Bytes));

    private Task<Dictionary<string, ArtifactMeasurement>> MeasureAsync(CancellationToken ct, IReadOnlySet<string>? excluded = null) =>
        MeasureArtifactsAsync(ct, excluded, null);

    private async Task<Dictionary<string, ArtifactMeasurement>> MeasureArtifactsAsync(CancellationToken ct, IReadOnlySet<string>? excluded,
        ArtifactVerificationSession? verification, IReadOnlyList<ArtifactPublication>? removedArtifacts = null)
    {
        using var measurement = LinerNotes.Application.Common.Diagnostics.DiscoveryDiagnostics.Measure("storage.artifact_scan");
        LinerNotes.Application.Common.Diagnostics.DiscoveryDiagnostics.NamespaceScan();
        var layout = await PublicationLayoutAsync(excluded, ct).ConfigureAwait(false);
        excluded = layout.Excluded;
        foreach (var removedArtifact in removedArtifacts ?? [])
        {
            ValidatePublicationPaths(removedArtifact);
            layout.Deletions[Path.Combine(removedArtifact.Root, removedArtifact.FinalName)] = removedArtifact;
        }
        var measurements = new Dictionary<string, ArtifactMeasurement>(StringComparer.Ordinal);
        long sharedBytes = 0;
        int namespaceEntries = 0;
        try
        {
            foreach (var category in Categories)
            {
                long total = 0;
                int fileCount = 0;
                using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                foreach (var root in options.ArtifactRoots[category].Order(StringComparer.Ordinal))
                {
                    if (!Path.IsPathFullyQualified(root) || !Directory.Exists(root)) throw new GenerationStoppedException("unknown_artifact_root");
                    var pending = new Stack<string>(); pending.Push(root);
                    var batch = new List<(string Path, long Length, ArtifactPublication? Deletion)>(2);
                    while (pending.TryPop(out var directory))
                    {
                        ct.ThrowIfCancellationRequested();
                        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new GenerationStoppedException("symlink_artifact_root");
                        var entries = Directory.EnumerateFileSystemEntries(directory).Take(100_001).ToHashSet(StringComparer.Ordinal);
                        if (entries.Count > 100_000) throw new GenerationStoppedException("storage_file_limit");
                        foreach (var child in entries.Concat(layout.Deletions.Keys.Where(path => Path.GetDirectoryName(path) == Path.TrimEndingDirectorySeparator(directory)))
                            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
                        {
                            if (++namespaceEntries > 100_000) throw new GenerationStoppedException("storage_file_limit");
                            if (!entries.Contains(child) && layout.Deletions.TryGetValue(child, out var deletion))
                            {
                                total = checked(total + deletion.ContentBytes);
                                sharedBytes = checked(sharedBytes + deletion.ContentBytes);
                                batch.Add((child, deletion.ContentBytes, deletion));
                                if (batch.Count == Math.Min(2, Environment.ProcessorCount)) await FlushAsync();
                                continue;
                            }
                            var attributes = File.GetAttributes(child);
                            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new GenerationStoppedException("symlink_artifact");
                            if ((attributes & FileAttributes.Directory) != 0) { pending.Push(child); continue; }
                            if (excluded?.Contains(child) == true) continue;
                            if (++fileCount > 100_000) throw new GenerationStoppedException("storage_file_limit");
                            long length = new FileInfo(child).Length;
                            sharedBytes = checked(sharedBytes + length); total = checked(total + length);
                            if (sharedBytes >= StopBytes) throw new GenerationStoppedException("storage_headroom");
                            batch.Add((child, length, null));
                            if (batch.Count == Math.Min(2, Environment.ProcessorCount)) await FlushAsync();
                        }
                        if (batch.Count > 0) await FlushAsync();
                    }
                    digest.AppendData(Encoding.UTF8.GetBytes($"root:{root.Length}:{root}"));

                    async Task FlushAsync()
                    {
                        // Hash independent files concurrently, then fold in the original namespace order.
                        // WhenAll waits for both reads even on failure; no I/O outlives its verification scope.
                        var results = await Task.WhenAll(batch.Select(async item =>
                        {
                            if (item.Deletion is { } deletion)
                                return (item.Path, item.Length, Ticks: deletion.LastWriteTimeUtcTicks, Hash: Convert.FromHexString(deletion.Sha256));
                            byte[] hash;
                            if (verification is not null) hash = await verification.HashAsync(item.Path, item.Length, ct).ConfigureAwait(false);
                            else
                            {
                                await using var input = OperatingSystem.IsLinux() ? DurableArtifactFile.OpenRead(item.Path) :
                                    new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
                                if (input.Length != item.Length) throw new GenerationStoppedException("artifact_changed_during_inventory");
                                hash = await ArtifactVerificationSession.HashFullContentAsync(input, ct).ConfigureAwait(false);
                                if (input.Length != item.Length) throw new GenerationStoppedException("artifact_changed_during_inventory");
                            }
                            var info = new FileInfo(item.Path);
                            if (info.Length != item.Length) throw new GenerationStoppedException("artifact_changed_during_inventory");
                            return (item.Path, item.Length, info.LastWriteTimeUtc.Ticks, Hash: hash);
                        }));
                        foreach (var result in results)
                        {
                            digest.AppendData(Encoding.UTF8.GetBytes($"{result.Path.Length}:{result.Path}:{result.Length}:{result.Ticks}:"));
                            digest.AppendData(result.Hash);
                        }
                        batch.Clear();
                    }
                }
                measurements.Add(category, new(total, Convert.ToHexStringLower(digest.GetHashAndReset())));
            }
            verification?.Validate();
        }
        catch (IOException) { throw new GenerationStoppedException("artifact_inventory_unavailable"); }
        catch (UnauthorizedAccessException) { throw new GenerationStoppedException("artifact_inventory_unavailable"); }
        return measurements;
    }

    private sealed class EmailLease(LocalGenerationStorage owner, FileStream file, GenerationInventory? inventory,
        Dictionary<string, ArtifactMeasurement>? baseline, HashSet<string>? ownedPaths, long allowance,
        ArtifactVerificationSession verification) : ILocalEmailWriteLease
    {
        private readonly Dictionary<string, string> ownRevisions = new(StringComparer.Ordinal);
        private static async Task<string> RevisionAsync(string path, CancellationToken ct)
        {
            if (!File.Exists(path)) return "absent";
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
            if (stream.Length > 1_200_000) throw new GenerationStoppedException("storage_headroom");
            return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
        }
        public async Task RecordOwnWriteAsync(string path, CancellationToken ct)
        {
            if (ownedPaths?.Contains(path) != true) throw new GenerationStoppedException("email_reservation_invalid");
            ownRevisions[path] = await RevisionAsync(path, ct);
        }
        public async Task ValidateAsync(GenerationStorageState database, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (inventory is null) return; // Receipt reconciliation creates no file and is available at the stop.
            owner.CheckInventory(inventory);
            if (database.DatabaseBytes < 0 || database.DatabaseBytes > inventory.DatabaseBytes || !LegacyRevisionEquals(database.Revision, inventory.DatabaseRevision))
                throw new GenerationStoppedException("database_inventory_changed");
            var actual = await owner.MeasureArtifactsAsync(ct, ownedPaths, verification).ConfigureAwait(false);
            if (Categories.Any(c => actual[c] != baseline![c])) throw new GenerationStoppedException("artifact_inventory_changed");
            foreach (var path in ownedPaths!)
                if (await RevisionAsync(path, ct) != ownRevisions.GetValueOrDefault(path, "absent"))
                    throw new GenerationStoppedException("email_owned_artifact_changed");
            long ownedBytes = ownedPaths!.Where(File.Exists).Sum(path => new FileInfo(path).Length);
            if (ownedBytes > allowance || checked(Total(inventory) + ownedBytes) >= StopBytes)
                throw new GenerationStoppedException("storage_headroom");
        }
        public ValueTask DisposeAsync() { verification.Dispose(); return file.DisposeAsync(); }
    }

    private sealed class Lease(LocalGenerationStorage owner, FileStream file, GenerationInventory inventory, bool coordinatedMode = false,
        ArtifactVerificationSession? verification = null, IDisposable? admission = null) : ICoordinatedStorageLease
    {
        public bool UsesCommitWitness => owner.commits is not null;
        private ManagedStorageCommit? committedWitness;
        private StoragePublicationPlan? artifactPlan;
        public StoragePublicationPlan CreatePublicationPlan() => artifactPlan ?? owner.PublicationPlan(inventory);
        public async Task ApplyPublishedArtifactAsync(ArtifactPublication operation, IGenerationStorageReservation reservation, CancellationToken ct)
        {
            if (owner.publications is null || reservation is not Reservation owned || !owned.BelongsTo(owner))
                throw new GenerationStoppedException("artifact_reservation_invalid");
            owner.ValidatePublicationPaths(operation);
            var committed = await owner.publications.FindAsync(operation.Id, ct);
            if (operation.Phase != ArtifactPublicationPhase.Publishing ||
                committed != (operation with { Phase = ArtifactPublicationPhase.Published, Version = operation.Version + 1 }))
                throw new GenerationStoppedException("artifact_publication_fenced");
            string final = Path.Combine(operation.Root, operation.FinalName);
            var unowned = await owner.MeasureArtifactsAsync(ct, new HashSet<string>(StringComparer.Ordinal) { final }, verification);
            if (Categories.Any(category => unowned[category] != inventory.Categories[category]))
                throw new GenerationStoppedException("artifact_inventory_changed");
            await VerifyPublicationContentAsync(final, operation, ct);
            var measured = await owner.MeasureArtifactsAsync(ct, null, verification);
            var before = inventory;
            inventory = inventory with { Categories = measured,
                ReservedArtifactBytes = checked(inventory.ReservedArtifactBytes - operation.ReservedBytes) };
            if (inventory.ReservedArtifactBytes < 0) throw new GenerationStoppedException("artifact_reservation_invalid");
            owned.AcceptInventory(inventory);
            artifactPlan = owner.PublicationPlan(inventory) with
            {
                BeforeInventoryJson = JsonSerializer.Serialize(before),
                OwnedChangesJson = JsonSerializer.Serialize(new[] { new { operation.Id } })
            };
        }
        public void BindCommit(ManagedStorageCommit commit) => committedWitness = commit;
        public async Task RebaseAsync(GenerationStorageState database, CancellationToken ct)
        {
            var current = await owner.ReadTrustedInventoryAsync(ct).ConfigureAwait(false);
            owner.CheckInventory(current);
            if (current.DatabaseRevision != database.Revision || current.Categories.Count != inventory.Categories.Count ||
                Categories.Any(category => current.Categories[category] != inventory.Categories[category]))
                throw new GenerationStoppedException("database_inventory_changed");
            // Shared namespace ownership excludes managed artifact transitions. ReserveAsync performs
            // the next full integrity checkpoint, including external changes, before any SQL mutation.
            inventory = current with { DatabaseBytes = database.DatabaseBytes, ReservedArtifactBytes = database.ReservedArtifactBytes };
            if (owner.commits is not null)
            {
                GenerationInventory? published = null;
                try { published = await owner.ReadInventoryAsync(ct).ConfigureAwait(false); }
                catch (GenerationStoppedException) { }
                if (published is null || published.CommitId != inventory.CommitId || published.DatabaseRevision != inventory.DatabaseRevision)
                {
                    await owner.ValidateArtifactsAsync(inventory, ct, verification).ConfigureAwait(false);
                    await owner.WriteKnownInventoryAsync(inventory, ct).ConfigureAwait(false);
                }
            }
        }
        public async Task<IGenerationStorageReservation> ReserveAsync(long serializedBytes, int pickCount,
            GenerationStorageState database, CancellationToken cancellationToken = default)
        {
            await owner.ValidateAsync(inventory, database, cancellationToken, coordinatedMode, verification).ConfigureAwait(false);
            if (serializedBytes < 0 || pickCount is < 0 or > 5) throw new GenerationStoppedException("invalid_storage_reservation");
            long allowance = checked(serializedBytes + owner.OverheadBytesPerPick * Math.Max(pickCount, 1));
            if (coordinatedMode) owner.coordinated!.RequireHeadroom(checked(allowance + inventory.ReservedArtifactBytes), owner.filesystemProbe);
            if (checked(Total(inventory) + allowance) >= StopBytes) throw new GenerationStoppedException("storage_headroom");
            return new Reservation(owner, inventory, allowance, coordinatedMode, verification);
        }
        public Task PublishCommittedInventoryAsync(GenerationStorageState committed, CancellationToken ct)
        {
            if (committedWitness is not null)
            {
                if (committed.Revision != committedWitness.ExpectedRevision)
                    throw new GenerationStoppedException("storage_commit_witness_mismatch");
                inventory = inventory with { CommitId = committedWitness.CommitId };
            }
            return owner.PublishCommittedAsync(inventory, committed, ct, verification: verification);
        }
        public async ValueTask DisposeAsync()
        {
            verification?.Dispose();
            try { await file.DisposeAsync(); }
            finally { admission?.Dispose(); }
        }
    }

    private sealed class Reservation(LocalGenerationStorage owner, GenerationInventory inventory, long allowance, bool coordinatedMode = false,
        ArtifactVerificationSession? verification = null) : IGenerationStorageReservation
    {
        public bool BelongsTo(LocalGenerationStorage candidate) => ReferenceEquals(owner, candidate);
        public void AcceptInventory(GenerationInventory accepted) => inventory = accepted;
        public Task ValidateAsync(GenerationStorageState database, CancellationToken cancellationToken = default)
        {
            // The witness/ledger revision, not physical page allocation, identifies managed mutations.
            // Background relation growth must still consume capacity before the callback can write.
            // Legacy mode retains its conservative stop-on-growth contract.
            if (coordinatedMode && owner.commits is not null && database.Revision == inventory.DatabaseRevision &&
                database.DatabaseBytes > inventory.DatabaseBytes)
            {
                inventory = inventory with { DatabaseBytes = database.DatabaseBytes };
                if (checked(Total(inventory) + allowance) >= StopBytes)
                    throw new GenerationStoppedException("storage_headroom");
            }
            if (coordinatedMode) owner.coordinated!.RequireHeadroom(checked(allowance + inventory.ReservedArtifactBytes), owner.filesystemProbe);
            return owner.ValidateAsync(inventory, database, cancellationToken, coordinatedMode, verification);
        }
        public async Task VerifyStoredAsync(long actualColumnBytes, GenerationStorageState database, CancellationToken cancellationToken = default)
        {
            await owner.ValidateArtifactsAsync(inventory, cancellationToken, verification).ConfigureAwait(false);
            if (coordinatedMode) owner.coordinated!.RequireHeadroom(0, owner.filesystemProbe);
            long growth = Math.Max(0, checked(database.DatabaseBytes + database.ReservedArtifactBytes) - checked(inventory.DatabaseBytes + inventory.ReservedArtifactBytes));
            if (actualColumnBytes < 0 || actualColumnBytes > allowance || growth > allowance ||
                checked(Total(inventory) + Math.Max(growth, actualColumnBytes)) >= StopBytes)
                throw new GenerationStoppedException("stored_bytes_exceed_reservation");
        }
    }
}
