using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class DiscoveryCoordinationPostgresTests
{
    private async Task<(string Root, PausedArtifactWriter Writer)> EnablePausedArtifactWriterAsync()
    {
        string artifacts = await EnableArtifactProtocolAsync();
        var writer = new PausedArtifactWriter();
        await services.DisposeAsync();
        registrations.AddSingleton<IArtifactStagingWriter>(writer);
        services = registrations.BuildServiceProvider();
        return (artifacts, writer);
    }

    [LocalPostgresMigrationFact]
    public async Task SlowStagingDoesNotBlockIndependentDiscoveryWritesAndKeepsItsFullReservation()
    {
        var (artifacts, writer) = await EnablePausedArtifactWriterAsync();
        await using var stageScope = services.CreateAsyncScope();
        var stage = stageScope.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>()
            .StageAsync("reports", artifacts, "slow.txt", new byte[65_536], default);
        try
        {
            await writer.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(stage.IsCompleted);
            await using (var recovery = services.CreateAsyncScope())
                await recovery.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>().RecoverAsync(default).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.False(stage.IsCompleted);
            Assert.NotNull(await GuestAsync('d').WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(stage.IsCompleted);
            await using var check = services.CreateAsyncScope();
            Assert.Equal(65_536, (await check.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync()).ReservedArtifactBytes);
        }
        finally { writer.Release.TrySetResult(); }
        var operation = await stage;
        await stageScope.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>().DeleteAsync(operation, default);
        await WriteAsync((_, _) => Task.FromResult(true), 0);
    }

    [LocalPostgresMigrationFact]
    public async Task CancellationDuringStagingLeavesARecoverableOwnedPartialAndNeverPublishes()
    {
        var (artifacts, writer) = await EnablePausedArtifactWriterAsync();
        using var cancellation = new CancellationTokenSource();
        await using var stageScope = services.CreateAsyncScope();
        var stage = stageScope.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>()
            .StageAsync("reports", artifacts, "cancel.txt", new byte[65_536], cancellation.Token);
        await writer.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stage);
        Assert.False(File.Exists(Path.Combine(artifacts, "cancel.txt")));
        Assert.Single(Directory.GetFiles(artifacts));
        await using var recovery = services.CreateAsyncScope();
        await recovery.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>().RecoverAsync(default);
        Assert.Empty(Directory.GetFiles(artifacts));
        Assert.Equal(0, (await recovery.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync()).ReservedArtifactBytes);
        await recovery.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }

    [LocalPostgresMigrationFact]
    public async Task InjectedDiskFullDuringStagingRetainsItsChargeUntilRecoveryRemovesThePartial()
    {
        string artifacts = await EnableArtifactProtocolAsync();
        await services.DisposeAsync();
        registrations.AddSingleton<IArtifactStagingWriter>(new DiskFullArtifactWriter());
        services = registrations.BuildServiceProvider();
        await using (var stage = services.CreateAsyncScope())
            await Assert.ThrowsAsync<IOException>(() => stage.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>()
                .StageAsync("reports", artifacts, "disk-full.txt", new byte[4096], default));
        await using var recovery = services.CreateAsyncScope();
        Assert.Equal(4096, (await recovery.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync()).ReservedArtifactBytes);
        Assert.False(File.Exists(Path.Combine(artifacts, "disk-full.txt")));
        await recovery.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>().RecoverAsync(default);
        Assert.Empty(Directory.GetFiles(artifacts));
        Assert.Equal(0, (await recovery.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync()).ReservedArtifactBytes);
        await recovery.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }
    private sealed class DiskFullArtifactWriter : IArtifactStagingWriter
    {
        public async Task WriteAsync(Stream target, ReadOnlyMemory<byte> content, CancellationToken ct)
        {
            await target.WriteAsync(content[..17], ct);
            await target.FlushAsync(ct);
            throw new IOException("Injected ENOSPC fixture", 28);
        }
    }

    private sealed class PausedArtifactWriter : IArtifactStagingWriter
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task WriteAsync(Stream target, ReadOnlyMemory<byte> content, CancellationToken ct)
        {
            int half = content.Length / 2;
            await target.WriteAsync(content[..half], ct);
            await target.FlushAsync(ct);
            Started.TrySetResult();
            await Release.Task.WaitAsync(ct);
            await target.WriteAsync(content[half..], ct);
        }
    }
}
