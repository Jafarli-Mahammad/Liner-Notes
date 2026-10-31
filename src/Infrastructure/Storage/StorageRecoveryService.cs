using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LinerNotes.Infrastructure.Storage;

/// <summary>Bounded recovery continues when consumers are paused, including privacy cleanup interrupted after SQL commit.</summary>
internal sealed class StorageRecoveryService(IServiceScopeFactory scopes, DiscoveryRuntimeOptions runtime,
    GenerationStorageOptions options, ILogger<StorageRecoveryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!runtime.ManagedWritesEnabled || string.IsNullOrWhiteSpace(options.InventoryPath)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            budget.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IRecoverableArtifactPublisher>().RecoverAsync(budget.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // A failed sweep retains its durable ownership and charges; never log private paths or identities.
                logger.LogWarning("Managed storage recovery is incomplete; retained operations will be retried.");
            }
        }
    }
}
