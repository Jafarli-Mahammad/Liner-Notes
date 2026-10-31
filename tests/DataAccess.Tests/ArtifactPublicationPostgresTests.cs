using System.Security.Cryptography;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class DiscoveryCoordinationPostgresTests
{
    private async Task<string> EnableArtifactProtocolAsync()
    {
        string artifacts = Path.Combine(root, "managed-reports");
        Directory.CreateDirectory(artifacts);
        services.GetRequiredService<GenerationStorageOptions>().ArtifactRoots["reports"] = [artifacts];
        await EnableDurableProtocolAsync();
        return artifacts;
    }

    private async Task CompleteArtifactAsync(ArtifactPublication operation, bool rollback = false)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        await provider.GetRequiredService<IDiscoveryWriteCoordinator>().PublishArtifactAsync(operation, 65_536, async ct =>
        {
            await provider.GetRequiredService<IRecoverableArtifactPublisher>().CompleteWithinAdmissionAsync(operation, ct);
            if (rollback) throw new InvalidOperationException("injected artifact rollback");
            return true;
        }, default);
    }

    [LocalPostgresMigrationFact]
    public async Task ArtifactPublicationReservesStagesPublishesAndTransfersItsChargeExactlyOnce()
    {
        string artifacts = await EnableArtifactProtocolAsync();
        byte[] bytes = "durable artifact fixture"u8.ToArray();
        ArtifactPublication operation;
        await using (var scope = services.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>();
            operation = await publisher.StageAsync("reports", artifacts, "fixture.txt", bytes, default);
            Assert.Equal(ArtifactPublicationPhase.Prepared, operation.Phase);
            Assert.False(File.Exists(Path.Combine(artifacts, operation.FinalName)));
            var state = await scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync();
            Assert.Equal(bytes.Length, state.ReservedArtifactBytes);
            operation = await publisher.PublishAsync(operation, default);
        }
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(artifacts, operation.FinalName)));
        await CompleteArtifactAsync(operation);
        await using var check = services.CreateAsyncScope();
        Assert.Equal(0, (await check.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync()).ReservedArtifactBytes);
        Assert.Equal(ArtifactPublicationPhase.Published, (await check.ServiceProvider.GetRequiredService<IArtifactPublicationStore>().FindAsync(operation.Id, default))!.Phase);
        await check.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        await WriteAsync((_, _) => Task.FromResult(true), 0);
    }

    [LocalPostgresMigrationFact]
    public async Task PublishedFileAndReservationSurviveBusinessRollbackAndAFreshScopeCompletesThem()
    {
        string artifacts = await EnableArtifactProtocolAsync();
        ArtifactPublication operation;
        await using (var scope = services.CreateAsyncScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>();
            operation = await publisher.StageAsync("reports", artifacts, "rollback.txt", "rollback fixture"u8.ToArray(), default);
            operation = await publisher.PublishAsync(operation, default);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => CompleteArtifactAsync(operation, rollback: true));
        await using (var check = services.CreateAsyncScope())
        {
            var resumed = await check.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>().PublishAsync(operation, default);
            Assert.Equal(operation, resumed);
            Assert.Equal(ArtifactPublicationPhase.Publishing, resumed.Phase);
        }
        await CompleteArtifactAsync(operation);
        await Assert.ThrowsAsync<GenerationStoppedException>(() => CompleteArtifactAsync(operation));
        await WriteAsync((_, _) => Task.FromResult(true), 0);
    }

    [LocalPostgresMigrationFact]
    public async Task RecoveryDiscardsOnlyRecordedUnpreparedStagingAndCanRepeat()
    {
        string artifacts = await EnableArtifactProtocolAsync();
        Guid id = Guid.NewGuid();
        var operation = new ArtifactPublication(id, "reports", artifacts, "recover.txt", id.ToString("N") + ".staged",
            1024, 1024, Convert.ToHexStringLower(SHA256.HashData(new byte[1024])), ArtifactPublicationPhase.Reserved, 0);
        await WriteAsync(async (provider, ct) =>
        {
            await provider.GetRequiredService<IArtifactPublicationStore>().ReserveAsync(operation, ct);
            return true;
        }, 65_536);
        await File.WriteAllBytesAsync(Path.Combine(artifacts, operation.StageName), new byte[17]);
        await using var scope = services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>();
        await publisher.RecoverAsync(default);
        await publisher.RecoverAsync(default);
        Assert.False(File.Exists(Path.Combine(artifacts, operation.StageName)));
        Assert.Equal(ArtifactPublicationPhase.Abandoned, (await scope.ServiceProvider.GetRequiredService<IArtifactPublicationStore>().FindAsync(id, default))!.Phase);
        Assert.Equal(0, (await scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync()).ReservedArtifactBytes);
        await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }

    [LocalPostgresMigrationFact]
    public async Task PreparedStagingTamperingWithRestoredSizeAndTimeFailsClosed()
    {
        string artifacts = await EnableArtifactProtocolAsync();
        await using var scope = services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>();
        var operation = await publisher.StageAsync("reports", artifacts, "tamper.txt", "original fixture"u8.ToArray(), default);
        string path = Path.Combine(artifacts, operation.StageName);
        DateTime original = File.GetLastWriteTimeUtc(path);
        byte[] bytes = await File.ReadAllBytesAsync(path); bytes[0] ^= 1;
        await File.WriteAllBytesAsync(path, bytes); File.SetLastWriteTimeUtc(path, original);
        var failure = await Assert.ThrowsAsync<GenerationStoppedException>(() => publisher.PublishAsync(operation, default));
        Assert.Equal("artifact_publication_checksum_invalid", failure.Message);
        Assert.False(File.Exists(Path.Combine(artifacts, operation.FinalName)));
    }
}
