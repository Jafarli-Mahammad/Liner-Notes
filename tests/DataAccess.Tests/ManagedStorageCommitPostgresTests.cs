using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class DiscoveryCoordinationPostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task CommitWitnessAccountsForItsOwnRevisionAndVariableSizedPayload()
    {
        foreach (int size in new[] { 1, 1024, 16000, 2 })
        {
            var id = Guid.NewGuid();
            await using var check = services.CreateAsyncScope();
            var store = check.ServiceProvider.GetRequiredService<IManagedStorageCommitStore>();
            var plan = new StoragePublicationPlan(JsonSerializer.Serialize(new { value = new string('x', size) }),
                "{}", "[]", new string('a', 64), DateTimeOffset.UtcNow);
            // Establish owns the transaction; no outer coordinator replaces its final witness.
            await store.EstablishAsync(id, plan, default);
            var saved = await store.ReadAsync(default) ?? throw new InvalidOperationException("missing witness");
            var actual = await check.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync();
            Assert.Equal(id, saved.CommitId);
            Assert.Equal(saved.ExpectedRevision, actual.Revision);
            await check.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
            await ReconcileAsync();
        }
    }

    [LocalPostgresMigrationFact]
    public async Task CommitWitnessRollsBackWithItsTransaction()
    {
        var id = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync<bool>(async (scope, ct) =>
        {
            var state = await scope.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync(ct);
            await scope.GetRequiredService<IManagedStorageCommitStore>().RecordAsync(id,
                new("{}", "{}", "[]", new string('a', 64), DateTimeOffset.UtcNow), state.Revision.Split(':')[^1], ct);
            throw new InvalidOperationException("injected rollback");
        }));
        await using var check = services.CreateAsyncScope();
        Assert.NotEqual(id, (await check.ServiceProvider.GetRequiredService<IManagedStorageCommitStore>().ReadAsync(default))?.CommitId);
        await check.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }
}
