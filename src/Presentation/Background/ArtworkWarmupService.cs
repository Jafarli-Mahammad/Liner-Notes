using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;

namespace LinerNotes.Presentation.Background;

public sealed class ArtworkWarmupService(IServiceScopeFactory scopes, DiscoveryRuntimeOptions options,
    TimeProvider clock, ILogger<ArtworkWarmupService> logger, IArtworkQueue queue) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => !options.RunJobs ? Task.CompletedTask
        : Task.WhenAll(ScanAsync(stoppingToken), WorkAsync(stoppingToken));

    private async Task ScanAsync(CancellationToken stoppingToken)
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
                queue.Enqueue(recent.Items.Where(item => item.Origin == "Public").Select(item => item.RecordingId).ToArray());
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

    private async Task WorkAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var ids = await queue.TakeAsync(ct);
            bool retryable = false;
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromSeconds(90));
                await using var scope = scopes.CreateAsyncScope();
                var artwork = scope.ServiceProvider.GetRequiredService<IRecordingArtwork>();
                await artwork.WarmAsync(ids, deadline.Token, allowAcquisition: false);
                var eligible = await scope.ServiceProvider.GetRequiredService<IArtworkCatalog>()
                    .EligibleAsync(ids.Where(id => artwork.GetCached(id) is null).ToArray(), clock.GetUtcNow(), deadline.Token);
                await artwork.WarmAsync(eligible, deadline.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { retryable = true; logger.LogWarning("Artwork batch deferred after {FailureType}.", ex.GetType().Name); }
            finally { queue.Complete(ids, retryable); }
        }
    }
}
