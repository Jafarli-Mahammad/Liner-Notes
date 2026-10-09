using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.Infrastructure.Tests;

public sealed class GenerationStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "liner-phase6-storage-" + Guid.NewGuid().ToString("N"));
    private readonly Clock clock = new();
    private readonly GenerationStorageOptions options;
    public GenerationStorageTests()
    {
        Directory.CreateDirectory(root);
        options = new() { InventoryPath = Path.Combine(root, "inventory.json"), LeasePath = Path.Combine(root, "lease"),
            DatabaseOverheadBytesPerPick = 100,
            ArtifactRoots = LocalGenerationStorage.Categories.ToDictionary(c => c, c => new[] { Path.Combine(root, c) }) };
        foreach (var path in options.ArtifactRoots.Values.SelectMany(x => x)) Directory.CreateDirectory(path);
    }
    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public async Task Reservation_CountsAllCategoriesAndRejectsExactThreshold()
    {
        var storage = new LocalGenerationStorage(options, clock);
        var db = new GenerationStorageState(79_999_000, "db");
        foreach (var c in LocalGenerationStorage.Categories) await File.WriteAllTextAsync(Path.Combine(root, c, "data"), "0123456789");
        var inventory = await storage.ReconcileAsync(db);
        Assert.Equal(60, inventory.Categories.Values.Sum(v => v.Bytes));
        await using var lease = await storage.AcquireAsync(db);
        await lease.ReserveAsync(839, 1, db); // 79,999,999 is permitted.
        var error = await Assert.ThrowsAsync<GenerationStoppedException>(() => lease.ReserveAsync(840, 1, db));
        Assert.Equal("storage_headroom", error.Message);
    }

    [Fact]
    public async Task MissingUnreconciledStaleInventoryAndOverhead_FailClosed()
    {
        var storage = new LocalGenerationStorage(options, clock);
        var db = new GenerationStorageState(1000, "db");
        await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.AcquireAsync(db));
        await storage.ReconcileAsync(db);
        clock.Now += TimeSpan.FromMinutes(6);
        await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.AcquireAsync(db));
        clock.Now -= TimeSpan.FromMinutes(6);
        var inventory = JsonSerializer.Deserialize<GenerationInventory>(await File.ReadAllTextAsync(options.InventoryPath!))!;
        await File.WriteAllTextAsync(options.InventoryPath!, JsonSerializer.Serialize(inventory with { Reconciled = false }));
        await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.AcquireAsync(db));
        options.DatabaseOverheadBytesPerPick = null;
        await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.AcquireAsync(db));
    }

    [Fact]
    public async Task Lease_RejectsConcurrentProcessAndRevalidatesSameSizeArtifactChanges()
    {
        var storage = new LocalGenerationStorage(options, clock);
        var db = new GenerationStorageState(1000, "db");
        string file = Path.Combine(root, "recordings", "data");
        await File.WriteAllTextAsync(file, "before");
        await storage.ReconcileAsync(db);
        await using var lease = await storage.AcquireAsync(db);
        await Assert.ThrowsAsync<GenerationStoppedException>(() => new LocalGenerationStorage(options, clock).AcquireAsync(db));
        var reservation = await lease.ReserveAsync(100, 1, db);
        await File.WriteAllTextAsync(file, "after!");
        await Assert.ThrowsAsync<GenerationStoppedException>(() => reservation.ValidateAsync(db));
    }

    [Fact]
    public async Task DatabaseRevisionAndStoredSizes_AreCheckedIndependently()
    {
        var storage = new LocalGenerationStorage(options, clock);
        var db = new GenerationStorageState(1000, "db");
        await storage.ReconcileAsync(db);
        await using var lease = await storage.AcquireAsync(db);
        var reservation = await lease.ReserveAsync(100, 1, db);
        await Assert.ThrowsAsync<GenerationStoppedException>(() => reservation.ValidateAsync(db with { Revision = "changed" }));
        await Assert.ThrowsAsync<GenerationStoppedException>(() => reservation.VerifyStoredAsync(201, db));
        await Assert.ThrowsAsync<GenerationStoppedException>(() => reservation.VerifyStoredAsync(100, db with { DatabaseBytes = 1201 }));
        await reservation.VerifyStoredAsync(200, db with { DatabaseBytes = 1200 });
    }

    [Fact]
    public async Task CancellationAndSymlinkRoots_StopMeasurement()
    {
        var storage = new LocalGenerationStorage(options, clock);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.ReconcileAsync(new(1000, "db"), cancellation.Token));
        var link = Path.Combine(root, "recordings", "link");
        File.CreateSymbolicLink(link, Path.Combine(root, "inventory.json"));
        await Assert.ThrowsAsync<GenerationStoppedException>(() => storage.ReconcileAsync(new(1000, "db")));
    }

    [Fact]
    public async Task HostComposition_CredentialsCannotEnableHttpAndLiveOriginIsRejected()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["LastFm:ApiKey"] = "synthetic-test-value", ["LastFm:Mode"] = "LiveOnly" }).Build();
        using var services = new ServiceCollection().AddLogging().AddInfrastructure(config).BuildServiceProvider();
        using var scope = services.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<LinerNotes.Infrastructure.RecommendationSources.LastFm.ILastFmApiClient>();
        var response = await client.GetArtistTopTagsAsync("missing");
        Assert.Equal(EvidenceOrigin.Recorded, response.Reference.Origin);
        Assert.Contains(response.Gaps, g => g.Reason == "recording_request_not_available");
        var live = new GenerationConfiguration { Origin = EvidenceOrigin.Live };
        Assert.Throws<ArgumentException>(() => live.Scoring());
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
