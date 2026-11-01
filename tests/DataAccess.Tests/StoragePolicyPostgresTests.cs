using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class DiscoveryCoordinationPostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task WriterWithDifferentCapacityCannotUseExistingWitness()
    {
        await EnableDurableProtocolAsync();
        await using var scope = services.CreateAsyncScope();
        var before = (await scope.ServiceProvider.GetRequiredService<IManagedStorageCommitStore>().ReadAsync(default))!;
        scope.ServiceProvider.GetRequiredService<GenerationStorageOptions>().StopBytes = 2_000_000_000;
        var error = await Assert.ThrowsAsync<GenerationStoppedException>(() => GuestAsync('b'));
        Assert.Equal("storage_policy_changed", error.Message);
        Assert.Equal(before.CommitId, (await scope.ServiceProvider.GetRequiredService<IManagedStorageCommitStore>().ReadAsync(default))!.CommitId);
    }

    [LocalPostgresMigrationFact]
    public async Task EstablishmentOverQuotaRollsBackWitnessAndLedger()
    {
        await EnableDurableProtocolAsync();
        await using var scope = services.CreateAsyncScope();
        var commits = scope.ServiceProvider.GetRequiredService<IManagedStorageCommitStore>();
        var store = scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>();
        var before = (await commits.ReadAsync(default))!;
        var state = await store.ReadStorageAsync();
        var error = await Assert.ThrowsAsync<GenerationStoppedException>(() =>
            commits.EstablishAsync(Guid.NewGuid(),before.Plan,default,maximumDatabaseBytes:1));
        Assert.Equal("storage_headroom",error.Message);
        Assert.Equal(before.CommitId,(await commits.ReadAsync(default))!.CommitId);
        Assert.Equal(state.Revision,(await store.ReadStorageAsync()).Revision);
    }
}
