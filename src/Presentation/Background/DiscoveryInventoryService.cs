using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Domain.Scoring;

namespace LinerNotes.Presentation.Background;

public sealed class DiscoveryInventoryService(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<DiscoveryInventoryService> logger) : BackgroundService
{
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<DiscoveryRuntimeOptions>().Validate();
        var generation = scope.ServiceProvider.GetRequiredService<GenerationConfiguration>();
        generation.Scoring();
        if (generation.Origin == EvidenceOrigin.Recorded)
            throw new InvalidOperationException("Recorded evaluation cannot run as guest discovery.");
        scope.ServiceProvider.GetRequiredService<IRecommendationProviderResolver>().Resolve();
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MaintainDiscoveryStorage>().ExecuteAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<GuestLifecycle>().CleanupAsync(50, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // Saved results stay readable; unknown baselines require explicit operator reconciliation.
                logger.LogWarning("Discovery storage maintenance unavailable; writes may be stopped.");
            }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) return; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}
