using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Recommendation;

namespace LinerNotes.Infrastructure.Storage;

public sealed class GenerationStorageOptions
{
    public string? InventoryPath { get; set; }
    public string? LeasePath { get; set; }
    public long? DatabaseOverheadBytesPerPick { get; set; }
    public Dictionary<string, string[]> ArtifactRoots { get; set; } = new(StringComparer.Ordinal);
}

public sealed record ArtifactMeasurement(long Bytes, string Revision);
public sealed record GenerationInventory(DateTimeOffset MeasuredAtUtc, bool Reconciled,
    long DatabaseBytes, string DatabaseRevision, Dictionary<string, ArtifactMeasurement> Categories);

/// <summary>Explicit local roots and reconciled inventory under one cross-process batch lease.</summary>
public sealed class LocalGenerationStorage(GenerationStorageOptions options, TimeProvider clock) : IGenerationStorage
{
    private long OverheadBytesPerPick => options.DatabaseOverheadBytesPerPick!.Value;
    public const long StopBytes = 80_000_000;
    public static IReadOnlyList<string> Categories { get; } = ["recordings", "cache", "reports", "copies", "backups", "partials"];

    public async Task<IGenerationStorageLease> AcquireAsync(GenerationStorageState database, CancellationToken cancellationToken = default)
    {
        var lease = OpenLease();
        try
        {
            var inventory = await ReadInventoryAsync(cancellationToken).ConfigureAwait(false);
            await ValidateAsync(inventory, database, cancellationToken).ConfigureAwait(false);
            return new Lease(this, lease, inventory);
        }
        catch { await lease.DisposeAsync().ConfigureAwait(false); throw; }
    }

    // Explicit operator helper, never called automatically by generation.
    public async Task<GenerationInventory> ReconcileAsync(GenerationStorageState database, CancellationToken cancellationToken = default)
    {
        await using var lease = OpenLease();
        var measurements = await MeasureAsync(cancellationToken).ConfigureAwait(false);
        var inventory = new GenerationInventory(clock.GetUtcNow(), true, database.DatabaseBytes, database.Revision, measurements);
        await ValidateAsync(inventory, database, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(options.InventoryPath!, JsonSerializer.SerializeToUtf8Bytes(inventory), cancellationToken).ConfigureAwait(false);
        return inventory;
    }

    private FileStream OpenLease()
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
        try { return new FileStream(options.LeasePath!, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { throw new GenerationStoppedException("storage_lease_unavailable"); }
        catch (UnauthorizedAccessException) { throw new GenerationStoppedException("storage_lease_unavailable"); }
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

    private async Task ValidateAsync(GenerationInventory inventory, GenerationStorageState database, CancellationToken ct)
    {
        CheckInventory(inventory);
        if (database.DatabaseBytes < 0 || database.DatabaseBytes != inventory.DatabaseBytes || database.Revision != inventory.DatabaseRevision)
            throw new GenerationStoppedException("database_inventory_changed");
        await ValidateArtifactsAsync(inventory, ct).ConfigureAwait(false);
        if (Total(inventory) >= StopBytes) throw new GenerationStoppedException("storage_headroom");
    }

    private async Task ValidateArtifactsAsync(GenerationInventory inventory, CancellationToken ct)
    {
        CheckInventory(inventory);
        var actual = await MeasureAsync(ct).ConfigureAwait(false);
        if (Categories.Any(c => actual[c] != inventory.Categories[c])) throw new GenerationStoppedException("artifact_inventory_changed");
    }

    private static long Total(GenerationInventory inventory) => checked(inventory.DatabaseBytes + inventory.Categories.Values.Sum(v => v.Bytes));

    private async Task<Dictionary<string, ArtifactMeasurement>> MeasureAsync(CancellationToken ct)
    {
        var measurements = new Dictionary<string, ArtifactMeasurement>(StringComparer.Ordinal);
        long sharedBytes = 0;
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
                    while (pending.TryPop(out var directory))
                    {
                        ct.ThrowIfCancellationRequested();
                        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new GenerationStoppedException("symlink_artifact_root");
                        foreach (var child in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
                        {
                            var attributes = File.GetAttributes(child);
                            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new GenerationStoppedException("symlink_artifact");
                            if ((attributes & FileAttributes.Directory) != 0) { pending.Push(child); continue; }
                            if (++fileCount > 100_000) throw new GenerationStoppedException("storage_file_limit");
                            var info = new FileInfo(child);
                            long before = info.Length;
                            sharedBytes = checked(sharedBytes + before); total = checked(total + before);
                            if (sharedBytes >= StopBytes) throw new GenerationStoppedException("storage_headroom");
                            await using var stream = new FileStream(child, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
                            var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
                            info.Refresh();
                            if (stream.Length != before || info.Length != before) throw new GenerationStoppedException("artifact_changed_during_inventory");
                            digest.AppendData(Encoding.UTF8.GetBytes($"{child.Length}:{child}:{before}:{info.LastWriteTimeUtc.Ticks}:"));
                            digest.AppendData(hash);
                        }
                    }
                    digest.AppendData(Encoding.UTF8.GetBytes($"root:{root.Length}:{root}"));
                }
                measurements.Add(category, new(total, Convert.ToHexStringLower(digest.GetHashAndReset())));
            }
        }
        catch (IOException) { throw new GenerationStoppedException("artifact_inventory_unavailable"); }
        catch (UnauthorizedAccessException) { throw new GenerationStoppedException("artifact_inventory_unavailable"); }
        return measurements;
    }

    private sealed class Lease(LocalGenerationStorage owner, FileStream file, GenerationInventory inventory) : IGenerationStorageLease
    {
        public async Task<IGenerationStorageReservation> ReserveAsync(long serializedBytes, int pickCount,
            GenerationStorageState database, CancellationToken cancellationToken = default)
        {
            await owner.ValidateAsync(inventory, database, cancellationToken).ConfigureAwait(false);
            if (serializedBytes < 0 || pickCount is < 0 or > 5) throw new GenerationStoppedException("invalid_storage_reservation");
            long allowance = checked(serializedBytes + owner.OverheadBytesPerPick * Math.Max(pickCount, 1));
            if (checked(Total(inventory) + allowance) >= StopBytes) throw new GenerationStoppedException("storage_headroom");
            return new Reservation(owner, inventory, allowance);
        }
        public ValueTask DisposeAsync() => file.DisposeAsync();
    }

    private sealed class Reservation(LocalGenerationStorage owner, GenerationInventory inventory, long allowance) : IGenerationStorageReservation
    {
        public Task ValidateAsync(GenerationStorageState database, CancellationToken cancellationToken = default) =>
            owner.ValidateAsync(inventory, database, cancellationToken);
        public async Task VerifyStoredAsync(long actualColumnBytes, GenerationStorageState database, CancellationToken cancellationToken = default)
        {
            await owner.ValidateArtifactsAsync(inventory, cancellationToken).ConfigureAwait(false);
            long growth = Math.Max(0, database.DatabaseBytes - inventory.DatabaseBytes);
            if (actualColumnBytes < 0 || actualColumnBytes > allowance || growth > allowance ||
                checked(Total(inventory) + Math.Max(growth, actualColumnBytes)) >= StopBytes)
                throw new GenerationStoppedException("stored_bytes_exceed_reservation");
        }
    }
}
