using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LinerNotes.Infrastructure.Storage;

/// <summary>Refuse protocol downgrades and replay only a database-anchored inventory before consumers start.</summary>
internal sealed class StorageProtocolStartup(IServiceScopeFactory scopes, DiscoveryRuntimeOptions runtime,
    GenerationStorageOptions options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Hosts with no managed filesystem retain their existing startup contract. Such hosts
        // cannot participate in coordinated generation because admission rejects missing roots.
        if (string.IsNullOrWhiteSpace(options.InventoryPath)) return;
        await using var scope = scopes.CreateAsyncScope();
        var witness = await scope.ServiceProvider.GetRequiredService<IManagedStorageCommitStore>().ReadAsync(cancellationToken);
        if (!runtime.ManagedWritesEnabled)
        {
            if (witness is not null) throw new GenerationStoppedException("storage_protocol_downgrade_refused");
            return;
        }
        // An uninitialized witness requires explicit operator establishment, never automatic adoption.
        if (witness is null) throw new GenerationStoppedException("storage_commit_witness_uninitialized");
        await scope.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>()
            .ReclaimAsync(_ => Task.FromResult(true), cancellationToken);
        await scope.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>().RecoverAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
