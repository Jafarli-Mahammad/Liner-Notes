using LinerNotes.Application.Common.Diagnostics;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Catalog;
using LinerNotes.Infrastructure.Catalog;
using LinerNotes.Infrastructure.RecommendationSources.ListenBrainz;
using Microsoft.Extensions.Caching.Memory;

namespace LinerNotes.Infrastructure.Artwork;

/// <summary>Only retained public provenance can authorize a metadata refresh. Private history is never consulted.</summary>
public sealed class ArtworkPublicRevalidation(IArtworkCatalog catalog, IListenBrainzApiClient metadata,
    RecordingArtworkCache cache, ImportCatalog import, ListenBrainzOptions options, TimeProvider clock) : IArtworkPublicRevalidation
{
    public async Task RevalidateAsync(IReadOnlyList<Guid> recordingIds, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (recordingIds.Count > 50) throw new ArgumentException("artwork_batch_limit");
        if (options.Mode != ListenBrainzClientMode.LiveOnly || !options.LiveUseApproved) return;
        using var measurement = DiscoveryDiagnostics.Measure("artwork.revalidation");
        await cache.Revalidation.WaitAsync(ct);
        try
        {
            var fresh = await catalog.EligibleAsync(recordingIds, clock.GetUtcNow(), ct);
            var stale = recordingIds.Where(id => id != Guid.Empty && !fresh.Contains(id) &&
                !cache.Entries.TryGetValue("revalidation:" + id, out _)).Distinct().Order().ToArray();
            if (stale.Length == 0) return;
            var previouslyPublic = (await catalog.PreviouslyPublicAsync(stale, ct)).Intersect(stale).Order().ToArray();
            if (previouslyPublic.Length == 0) return;
            // Bound unsuccessful attempts independently of image negative entries and HTTP cache policy.
            // This stores only the attempt time, never a rejected provider payload.
            foreach (var id in previouslyPublic) cache.Entries.Set("revalidation:" + id, true,
                new MemoryCacheEntryOptions { Size = 128, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) });
            var request = new ListenBrainzRequest(ListenBrainzRequestKind.RecordingMetadata,
                string.Join(',', previouslyPublic.Select(id => id.ToString("D"))));
            var response = await metadata.SendAsync(request, ct);
            if (response.Gaps.Any(gap => gap.Reason is "rate_limit" or "transport_error" or "request_timeout" or "upstream_unavailable"))
                throw new DiscoverySourceException("artwork_public_revalidation_deferred", true);
            if (response.Reference.Provider != request.Provider || response.Reference.Method != request.Method ||
                response.Reference.RequestIdentity != request.Identity || response.SharedCacheUntilUtc <= clock.GetUtcNow()) return;
            // The existing catalog projection validates recording/artist identities and names and drops all tags.
            var documents = ListenBrainzRecommendationProvider.ReadPublicRecordingMetadata([response], previouslyPublic.ToHashSet());
            if (documents.Count == 0) return;
            // Recheck provenance after the network wait; cleanup must not turn an arbitrary ID into an authorization.
            var retainedProof = (await catalog.PreviouslyPublicAsync(previouslyPublic, ct)).ToHashSet();
            if (documents.Any(document => document.MatchedCanonicalIds.Any(id => !retainedProof.Contains(id)))) return;
            await PublicRecordingCatalog.PublishAsync(documents, import, ct);
            // The worker queries current public eligibility again before preparing and acquiring images.
        }
        finally { cache.Revalidation.Release(); }
    }
}
