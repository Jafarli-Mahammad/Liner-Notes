using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Discovery;
using LinerNotes.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class DiscoveryCoordinationPostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task WitnessBoundAdmissionRefreshesPhysicalGrowthWithoutAcceptingChangedRowsOrExceedingQuota()
    {
        await EnableDurableProtocolAsync();
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var store = provider.GetRequiredService<IDigestGenerationStore>();
        await using var ownership = await store.AcquireBatchAsync();
        var before = await store.ReadStorageAsync();
        await using var lease = await provider.GetRequiredService<ICoordinatedGenerationStorage>().AcquireCoordinatedAsync(before, default);
        var reservation = await lease.ReserveAsync(8192, 0, before, default);
        // PostgreSQL may allocate relation pages without a managed row/schema change.
        await reservation.ValidateAsync(before with { DatabaseBytes = before.DatabaseBytes + 4096 }, default);
        var changed = await Assert.ThrowsAsync<GenerationStoppedException>(() => reservation.ValidateAsync(
            before with { Revision = before.Revision + "changed" }, default));
        Assert.Equal("database_inventory_changed", changed.Message);
        var pressure = await Assert.ThrowsAsync<GenerationStoppedException>(() => reservation.ValidateAsync(
            before with { DatabaseBytes = LocalGenerationStorage.StopBytes - 1 }, default));
        Assert.Equal("storage_headroom", pressure.Message);
    }
    private async Task EnableDurableProtocolAsync()
    {
        await services.DisposeAsync();
        registrations.AddSingleton(new DiscoveryRuntimeOptions { EnforceStorageAdmission = true });
        services = registrations.BuildServiceProvider();
        await ReconcileAsync();
    }

    [LocalPostgresMigrationFact]
    public async Task InventoryRecoveryRewritesATruncatedKnownCommitTemporaryFileAndLeavesUnknownFilesAlone()
    {
        await EnableDurableProtocolAsync();
        await using var scope = services.CreateAsyncScope();
        var witness = (await scope.ServiceProvider.GetRequiredService<IManagedStorageCommitStore>().ReadAsync(default))!;
        string inventory = Path.Combine(root, "inventory.json");
        string partial = inventory + "." + witness.CommitId.ToString("N") + ".tmp";
        string unknown = inventory + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(partial, "{\"interrupted\":");
        await File.WriteAllTextAsync(unknown, "unrelated fixture");
        File.Delete(inventory);
        await WriteAsync((_, _) => Task.FromResult(true), 0);
        Assert.False(File.Exists(partial));
        Assert.Equal("unrelated fixture", await File.ReadAllTextAsync(unknown));
        var recovered = JsonSerializer.Deserialize<GenerationInventory>(await File.ReadAllTextAsync(inventory))!;
        Assert.Equal(witness.CommitId, recovered.CommitId);
        await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }

    [LocalPostgresMigrationFact]
    public async Task CommittedDatabaseMutationSurvivesInventoryPublicationFailureAndRecoversFromItsWitness()
    {
        await EnableDurableProtocolAsync();
        Guid id = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<GenerationStoppedException>(() => WriteAsync(async (scope, ct) =>
        {
            var profile = ListenerProfile.Guest(DateTimeOffset.UtcNow, TimeSpan.FromDays(7));
            id = profile.Id;
            var db = scope.GetRequiredService<DataContext>();
            db.ListenerProfiles.Add(profile);
            await db.SaveChangesAsync(ct);
            // All SQL changes and witness may commit, but the inventory rename cannot succeed.
            File.Delete(Path.Combine(root, "inventory.json"));
            Directory.CreateDirectory(Path.Combine(root, "inventory.json"));
            return id;
        }));
        Assert.Equal("committed_inventory_publication_failed", error.Message);
        await using (var check = services.CreateAsyncScope())
        {
            Assert.True(await check.ServiceProvider.GetRequiredService<DataContext>().ListenerProfiles.AnyAsync(p => p.Id == id));
            await check.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        }
        Directory.Delete(Path.Combine(root, "inventory.json"));
        // A fresh scope has no in-memory reservation or retry state from the failed publisher.
        await WriteAsync((_, _) => Task.FromResult(true), 0);
        var published = JsonSerializer.Deserialize<GenerationInventory>(await File.ReadAllTextAsync(Path.Combine(root, "inventory.json")))!;
        await using var verified = services.CreateAsyncScope();
        var witness = await verified.ServiceProvider.GetRequiredService<IManagedStorageCommitStore>().ReadAsync(default);
        Assert.Equal(witness!.CommitId, published.CommitId);
        Assert.Equal(witness.ExpectedRevision, published.DatabaseRevision);
        await WriteAsync((_, _) => Task.FromResult(true), 0); // idempotent replay
    }

    [LocalPostgresMigrationFact]
    public async Task StartupRefusesAnInitializedDatabaseInLegacyMode()
    {
        await EnableDurableProtocolAsync();
        await services.DisposeAsync();
        registrations.AddSingleton(new DiscoveryRuntimeOptions());
        services = registrations.BuildServiceProvider();
        var startup = Assert.Single(services.GetServices<IHostedService>(), service => service.GetType().Name == "StorageProtocolStartup");
        var error = await Assert.ThrowsAsync<GenerationStoppedException>(() => startup.StartAsync(default));
        Assert.Equal("storage_protocol_downgrade_refused", error.Message);
    }
}
