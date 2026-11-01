using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Infrastructure.Artwork;

namespace LinerNotes.Presentation.Background;

public sealed class ArtistArtworkService(IServiceScopeFactory scopes, ArtistArtworkCache cache,
    DiscoveryRuntimeOptions options, ILogger<ArtistArtworkService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.RunJobs) return;
        while (!ct.IsCancellationRequested)
        {
            var id = await cache.TakeAsync(ct);
            ArtistPhoto? photo = null;
            var retryable = false;
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromSeconds(45));
                await using var scope = scopes.CreateAsyncScope();
                photo = await scope.ServiceProvider.GetRequiredService<IArtistArtworkAcquirer>().AcquireAsync(id, deadline.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                retryable = true;
                logger.LogWarning("Artist artwork deferred after {FailureType}.", ex.GetType().Name);
            }
            finally { cache.Complete(id, photo, retryable); }
        }
    }
}
