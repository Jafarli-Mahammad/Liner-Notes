using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class DiscoveryCoordinationPostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task MigrationRollbackRefusesAnActivePublicationAndLeavesItsWitnessUsable()
    {
        string artifacts = await EnableArtifactProtocolAsync();
        await using var scope = services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>();
        var operation = await publisher.StageAsync("reports", artifacts, "pending.txt", "pending fixture"u8.ToArray(), default);
        var db = scope.ServiceProvider.GetRequiredService<LinerNotes.DataAccess.Persistence.AppDbContext>();
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.GetService<IMigrator>()
            .MigrateAsync("20261029193819_BatchManagedStorageAccounting"));
        Assert.Equal("P0001", error.SqlState);
        Assert.Equal(operation, await scope.ServiceProvider.GetRequiredService<IArtifactPublicationStore>().FindAsync(operation.Id, default));
        await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        await publisher.DeleteAsync(operation, default);
        await WriteAsync((_, _) => Task.FromResult(true), 0);
    }
    private async Task<ArtifactPublication> CreatePublishedArtifactAsync(string artifacts, string name)
    {
        await using var scope = services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>();
        var operation = await publisher.StageAsync("reports", artifacts, name, "deletion fixture"u8.ToArray(), default);
        operation = await publisher.PublishAsync(operation, default);
        await CompleteArtifactAsync(operation);
        return (await scope.ServiceProvider.GetRequiredService<IArtifactPublicationStore>().FindAsync(operation.Id, default))!;
    }

    [LocalPostgresMigrationFact]
    public async Task ArtifactDeletionReleasesOnlyItsKnownFileAndCanRepeat()
    {
        string artifacts = await EnableArtifactProtocolAsync();
        var operation = await CreatePublishedArtifactAsync(artifacts, "delete.txt");
        await using var scope = services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>();
        await publisher.DeleteAsync(operation, default);
        await publisher.DeleteAsync(operation, default);
        Assert.False(File.Exists(Path.Combine(artifacts, operation.FinalName)));
        Assert.Equal(ArtifactPublicationPhase.Deleted, (await scope.ServiceProvider.GetRequiredService<IArtifactPublicationStore>().FindAsync(operation.Id, default))!.Phase);
        await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        await WriteAsync((_, _) => Task.FromResult(true), 0);
    }

    [LocalPostgresMigrationFact]
    public async Task MissingFileAfterDurableDeletionIntentRemainsChargedUntilRecoveryCommits()
    {
        string artifacts = await EnableArtifactProtocolAsync();
        var operation = await CreatePublishedArtifactAsync(artifacts, "interrupted-delete.txt");
        await using (var prepare = services.CreateAsyncScope())
        {
            var store = prepare.ServiceProvider.GetRequiredService<IArtifactPublicationStore>();
            operation = await prepare.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ReclaimAsync(
                ct => store.TransitionAsync(operation.Id, operation.Version, ArtifactPublicationPhase.Published, ArtifactPublicationPhase.Deleting, ct), default);
        }
        File.Delete(Path.Combine(artifacts, operation.FinalName));
        await WriteAsync((_, _) => Task.FromResult(true), 0);
        var pendingInventory = System.Text.Json.JsonSerializer.Deserialize<GenerationInventory>(await File.ReadAllTextAsync(Path.Combine(root, "inventory.json")))!;
        Assert.Equal(operation.ContentBytes, pendingInventory.Categories["reports"].Bytes);
        await using var recovery = services.CreateAsyncScope();
        await recovery.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>().RecoverAsync(default);
        await recovery.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>().RecoverAsync(default);
        var complete = System.Text.Json.JsonSerializer.Deserialize<GenerationInventory>(await File.ReadAllTextAsync(Path.Combine(root, "inventory.json")))!;
        Assert.Equal(0, complete.Categories["reports"].Bytes);
        await recovery.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }

    [LocalPostgresMigrationFact]
    public async Task ParallelArtifactReservationsCannotOversubscribeTheTransactionalBudget()
    {
        string artifacts = await EnableArtifactProtocolAsync();
        const long amount = 64 * 1024 * 1024;
        var accepted = await Task.WhenAll(Enumerable.Range(0, 18).Select(async index =>
        {
            try
            {
                Guid id = Guid.NewGuid();
                var operation = new ArtifactPublication(id, "reports", artifacts, $"quota-{index}.txt", id.ToString("N") + ".staged",
                    amount, amount, new string('0', 64), ArtifactPublicationPhase.Reserved, 0);
                await WriteAsync(async (provider, ct) =>
                {
                    await provider.GetRequiredService<IArtifactPublicationStore>().ReserveAsync(operation, ct);
                    return true;
                }, amount);
                return true;
            }
            catch (GenerationStoppedException error) when (error.Message is "storage_headroom" or "stored_bytes_exceed_reservation")
            { return false; }
        }));
        Assert.Contains(true, accepted);
        Assert.Contains(false, accepted);
        await using var scope = services.CreateAsyncScope();
        var state = await scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync();
        Assert.Equal(accepted.Count(result => result) * amount, state.ReservedArtifactBytes);
        Assert.True(state.DatabaseBytes + state.ReservedArtifactBytes < LocalGenerationStorage.StopBytes);
        await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        await scope.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>().RecoverAsync(default);
        Assert.Equal(0, (await scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync()).ReservedArtifactBytes);
    }
}
