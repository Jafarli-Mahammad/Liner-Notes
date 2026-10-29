using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;

namespace LinerNotes.Presentation.Background;

public sealed class ArtworkWarmupService(IServiceScopeFactory scopes, DiscoveryRuntimeOptions options,
    TimeProvider clock, ILogger<ArtworkWarmupService> logger, IArtworkQueue queue) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => !options.RunJobs ? Task.CompletedTask
        : Task.WhenAll(ScanAsync(stoppingToken), PrepareAsync(stoppingToken), PrepareAsync(stoppingToken),
            AcquireAsync(stoppingToken), AcquireAsync(stoppingToken));

    private async Task ScanAsync(CancellationToken ct)
    {
        long? cursor = null;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var recent = await scope.ServiceProvider.GetRequiredService<IRecentRecommendations>().ReadAsync(cursor, 50, clock.GetUtcNow(), ct);
                queue.Enqueue(recent.Items.Where(item => item.Origin == "Public").Select(item => item.RecordingId).ToArray());
                cursor = recent.NextCursor;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Artwork scan deferred after {FailureType}.", ex.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(15), clock, ct);
        }
    }

    private async Task PrepareAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var ids = await queue.TakeAsync(ct);
            Guid[] transferred = [];
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
                var stale = ids.Where(id => artwork.GetCached(id) is null && !eligible.Contains(id)).ToArray();
                if (stale.Length > 0 && scope.ServiceProvider.GetService<IArtworkPublicRevalidation>() is { } revalidation)
                {
                    await revalidation.RevalidateAsync(stale, deadline.Token);
                    eligible = await scope.ServiceProvider.GetRequiredService<IArtworkCatalog>()
                        .EligibleAsync(ids.Where(id => artwork.GetCached(id) is null).ToArray(), clock.GetUtcNow(), deadline.Token);
                }
                var plans = (await artwork.PrepareAcquisitionAsync(eligible, deadline.Token))
                    .Where(plan => eligible.Contains(plan.RecordingId)).DistinctBy(plan => plan.RecordingId).ToArray();
                queue.QueuePrepared(plans);
                transferred = plans.Select(plan => plan.RecordingId).ToArray();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { retryable = true; logger.LogWarning("Artwork preparation deferred after {FailureType}.", ex.GetType().Name); }
            finally { queue.Complete(ids.Except(transferred).ToArray(), retryable); }
        }
    }

    private async Task AcquireAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var plan = await queue.TakePreparedAsync(ct);
            bool retryable = false;
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromSeconds(90));
                await using var scope = scopes.CreateAsyncScope();
                var artwork = scope.ServiceProvider.GetRequiredService<IRecordingArtwork>();
                // A different host may have persisted the cover while this item waited.
                await artwork.WarmAsync([plan.RecordingId], deadline.Token, allowAcquisition: false);
                if (artwork.GetCached(plan.RecordingId) is null)
                {
                    // Recheck public eligibility at acquisition, after the preparation handoff.
                    var eligible = await scope.ServiceProvider.GetRequiredService<IArtworkCatalog>()
                        .EligibleAsync([plan.RecordingId], clock.GetUtcNow(), deadline.Token);
                    if (eligible.Contains(plan.RecordingId)) await artwork.AcquireAsync(plan, deadline.Token);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { retryable = true; logger.LogWarning("Artwork acquisition deferred after {FailureType}.", ex.GetType().Name); }
            finally { queue.Complete([plan.RecordingId], retryable); }
        }
    }
}
