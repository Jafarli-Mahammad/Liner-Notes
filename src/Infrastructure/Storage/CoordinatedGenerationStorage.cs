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
    // Set by DI through a separate constructor, keeping legacy direct construction source-compatible.
    private readonly CoordinatedStorageOptions? coordinated;
    private readonly IStorageFreeSpaceProbe filesystemProbe = new DriveStorageFreeSpaceProbe();
    public LocalGenerationStorage(GenerationStorageOptions options, TimeProvider clock, CoordinatedStorageOptions coordinatedOptions, IStorageFreeSpaceProbe? probe = null)
        : this(options, clock) { coordinated = coordinatedOptions; filesystemProbe = probe ?? new DriveStorageFreeSpaceProbe(); }

    public async Task<ICoordinatedStorageLease> AcquireCoordinatedAsync(GenerationStorageState database, CancellationToken ct)
    {
        if (coordinated is null) throw new GenerationStoppedException("discovery_filesystem_inventory_unconfigured");
        coordinated.RequireHeadroom(0, filesystemProbe);
        var file = OpenLease();
        try
        {
            var inventory = await ReadInventoryAsync(ct);
            CheckInventory(inventory);
            if (!database.Revision.StartsWith("managed-v1:", StringComparison.Ordinal) || inventory.DatabaseRevision != database.Revision)
                throw new GenerationStoppedException("database_inventory_changed");
            await ValidateArtifactsAsync(inventory, ct);
            // Ledger/schema unchanged: charge current observed relation size, including background page growth.
            // Never silently accept an unaccounted row mutation or a changed schema/trigger inventory.
            inventory = inventory with { DatabaseBytes = database.DatabaseBytes };
            if (Total(inventory) >= StopBytes) throw new GenerationStoppedException("storage_headroom");
            return new Lease(this, file, inventory, coordinatedMode: true);
        }
        catch { await file.DisposeAsync(); throw; }
    }
    private async Task PublishCommittedAsync(GenerationInventory previous, GenerationStorageState committed, CancellationToken ct)
    {
        if (coordinated is null) throw new GenerationStoppedException("discovery_filesystem_inventory_unconfigured");
        coordinated.RequireHeadroom(0, filesystemProbe);
        await ValidateArtifactsAsync(previous, ct);
        var current = previous with { DatabaseBytes = committed.DatabaseBytes, DatabaseRevision = committed.Revision, MeasuredAtUtc = clock.GetUtcNow() };
        if (Total(current) >= StopBytes) throw new GenerationStoppedException("storage_headroom");
        string temporary = options.InventoryPath! + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(output, current, cancellationToken: ct);
                await output.FlushAsync(ct);
                output.Flush(flushToDisk: true);
            }
            File.Move(temporary, options.InventoryPath!, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new GenerationStoppedException("committed_inventory_publication_failed"); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
