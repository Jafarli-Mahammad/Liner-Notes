using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;

namespace LinerNotes.Presentation.Background;

public sealed class ArtworkWarmupService(IServiceScopeFactory scopes, DiscoveryRuntimeOptions options,
    TimeProvider clock, ILogger<ArtworkWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.RunJobs) return;
        long? cursor = null;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var recent = await scope.ServiceProvider.GetRequiredService<IRecentRecommendations>()
                    .ReadAsync(cursor, 50, clock.GetUtcNow(), stoppingToken);
                await scope.ServiceProvider.GetRequiredService<IRecordingArtwork>()
                    .WarmAsync(recent.Items.Where(item => item.Origin == "Public").Select(item => item.RecordingId).ToArray(), stoppingToken);
                cursor = recent.NextCursor;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // Provider exceptions can contain URLs; retain only the type.
                logger.LogWarning("Artwork warmup deferred after {FailureType}.", ex.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(15), clock, stoppingToken);
        }
    }
}
