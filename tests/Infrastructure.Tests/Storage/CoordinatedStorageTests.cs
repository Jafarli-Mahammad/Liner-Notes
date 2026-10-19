using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.Storage;
using Xunit;
namespace LinerNotes.Infrastructure.Tests.Storage;
public sealed class CoordinatedStorageTests : IAsyncLifetime
{
    [Fact]
    public async Task ReclamationWorksAtManagedCutoffButKeepsDiskAndRevisionGuards()
    {
        var over = Before with { DatabaseBytes = 1_000_000_001 };
        await storage.RefreshKnownInventoryAsync(over, default);
        await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.AcquireCoordinatedAsync(over, default));
        clock.Advance(TimeSpan.FromMinutes(6));
        await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.AcquireReclamationAsync(
            over with { Revision = "managed-v1:changed:0:fixture" }, default));
        await using (var reclaim = await storage.AcquireReclamationAsync(over, default))
        {
            probe.Free = 0;
            await Assert.ThrowsAsync<GenerationStoppedException>(() => reclaim.VerifyAsync(over, default));
            probe.Free = 2_000_000_000;
            await reclaim.VerifyAsync(over, default);
            await reclaim.PublishCommittedInventoryAsync(over, default);
        }
        await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.AcquireCoordinatedAsync(over, default));
        await using var nextDeletion = await storage.AcquireReclamationAsync(over, default);
    }
    [Fact]
    public async Task KnownInventoryCanRefreshAfterIdleWithoutAcceptingChangedRevision()
    {
        clock.Advance(TimeSpan.FromMinutes(6));
        await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.AcquireCoordinatedAsync(Before, default));
        await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.RefreshKnownInventoryAsync(
            Before with { Revision = "managed-v1:2:changed:fixture" }, default));
        await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.AcquireCoordinatedAsync(Before, default));

        var grown = Before with { DatabaseBytes = Before.DatabaseBytes + 4096 };
        await storage.RefreshKnownInventoryAsync(grown, default);
        await using var admitted = await storage.AcquireCoordinatedAsync(grown, default);
        await admitted.ReserveAsync(1024, 0, grown);
    }

    [Fact]
    public async Task MaintenanceRetainsFilesystemAndClockGuards()
    {
        clock.Advance(TimeSpan.FromMinutes(-1));
        await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.RefreshKnownInventoryAsync(Before, default));
        clock.Advance(TimeSpan.FromMinutes(7));
        probe.Free = 0;
        await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.RefreshKnownInventoryAsync(Before, default));
        probe.Free = 2_000_000_000;
        await storage.RefreshKnownInventoryAsync(Before, default);
        await using var admitted = await storage.AcquireCoordinatedAsync(Before, default);
    }
    private readonly string root = Path.Combine(Path.GetTempPath(), "liner-storage-c7-" + Guid.NewGuid().ToString("N"));
    private readonly Clock clock = new(); private readonly Probe probe = new();
    private LocalGenerationStorage storage = null!;
    private static readonly GenerationStorageState Before = new(10_000_000, "managed-v1:1:0:fixture");
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(root, "inventories"));
        storage = new(new() { InventoryPath = Path.Combine(root, "inventories", "inventory.json"), LeasePath = Path.Combine(root, "lease"),
            DatabaseOverheadBytesPerPick = 1_048_576, ArtifactRoots = LocalGenerationStorage.Categories.ToDictionary(c => c, _ => Array.Empty<string>()) },
            clock, new() { FilesystemPaths = [root], MinimumFreeBytes = 67_108_864 }, probe);
        await storage.ReconcileAsync(Before);
    }
    public Task DisposeAsync() { if (Directory.Exists(root)) Directory.Delete(root, true); return Task.CompletedTask; }
    [Fact]
    public async Task DiskBecomingFullAfterReservationStopsVerification()
    {
        await using var lease = await storage.AcquireCoordinatedAsync(Before, default);
        var reservation = await lease.ReserveAsync(1024, 0, Before);
        probe.Free = 0;
        var failure = await Assert.ThrowsAsync<GenerationStoppedException>(() => reservation.VerifyStoredAsync(1024, Before));
        Assert.Equal("filesystem_headroom", failure.Message);
    }
    [Fact]
    public async Task UnknownFilesystemAndStaleInventoryFailClosed()
    {
        probe.Fail = true;
        Assert.Equal("filesystem_inventory_unknown", (await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.AcquireCoordinatedAsync(Before, default))).Message);
        probe.Fail = false; clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal("stale_or_unknown_storage_inventory", (await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.AcquireCoordinatedAsync(Before, default))).Message);
    }
    [Fact]
    public async Task FailedPostCommitPublicationLeavesOldRevisionAndRequiresReconciliation()
    {
        var committed = Before with { DatabaseBytes = Before.DatabaseBytes + 4096, Revision = "managed-v1:2:200:fixture" };
        await using (var lease = await storage.AcquireCoordinatedAsync(Before, default))
        {
            Directory.Move(Path.Combine(root, "inventories"), Path.Combine(root, "moved"));
            var failure = await Assert.ThrowsAsync<GenerationStoppedException>(() => lease.PublishCommittedInventoryAsync(committed, default));
            Assert.Equal("committed_inventory_publication_failed", failure.Message);
            Directory.Move(Path.Combine(root, "moved"), Path.Combine(root, "inventories"));
        }
        Assert.Equal("database_inventory_changed", (await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.AcquireCoordinatedAsync(committed, default))).Message);
        await storage.ReconcileAsync(committed);
        await using var recovered = await storage.AcquireCoordinatedAsync(committed, default);
    }
    private sealed class Probe : IStorageFreeSpaceProbe
    {
        public long Free = 2_000_000_000; public bool Fail;
        public long AvailableBytes(string path) => Fail ? throw new IOException("Injected unavailable volume") : Free;
    }
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan delta) => now += delta;
    }
}
