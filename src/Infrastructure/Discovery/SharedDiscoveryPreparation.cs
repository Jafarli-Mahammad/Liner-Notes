using System.Security.Cryptography;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
using LinerNotes.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
namespace LinerNotes.Infrastructure.Discovery;

public sealed class SharedDiscoveryPreparation(IServiceScopeFactory scopes, DiscoveryMemoryCaches caches,
    GenerationConfiguration configuration, TimeProvider clock) : IDiscoveryPreparation, IDisposable
{
    // Fixed stripes bound synchronization memory independently of query diversity.
    private readonly SemaphoreSlim[] stripes = Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    public string KeyFor(DiscoveryPoolRequest request)
    {
        configuration.Scoring();
        if (configuration.Origin == EvidenceOrigin.Recorded) throw new GenerationStoppedException("evaluation_evidence_ineligible");
        if (string.IsNullOrWhiteSpace(request.Seed) || request.Seed.Length > 256 || request.Seed.Any(char.IsControl) ||
            request.ArtistId == Guid.Empty || (request.IsTag && request.ArtistId.HasValue) || (request.IsRecording && (request.ArtistId is null || request.IsTag))) throw new ArgumentException("invalid_pool_seed");
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Version = "public-pool-v1",
            Configuration = configuration,
            request.ArtistId,
            Seed = request.ArtistId.HasValue ? null : request.Seed.Trim().ToLowerInvariant(),
            request.IsTag,
            request.IsRecording
        })));
    }
    public PreparedDiscoveryPool? TryGet(DiscoveryPoolRequest request)
    {
        string key = "pool:" + KeyFor(request);
        return caches.Prepared.TryGetValue<PreparedDiscoveryPool>(key, out var hit) && hit!.ExpiresAtUtc > clock.GetUtcNow() ? hit : null;
    }
    public void Remember(PreparedDiscoveryPool pool)
    {
        if (pool.ExpiresAtUtc is not { } expiry || expiry <= clock.GetUtcNow()) return;
        string key = "pool:" + pool.Key;
        caches.Prepared.Set(key, pool, new MemoryCacheEntryOptions
        {
            Size = DiscoveryMemoryCaches.EstimateBytes(pool.SerializedBytes, key),
            AbsoluteExpirationRelativeToNow = expiry - clock.GetUtcNow()
        });
    }
    public async Task<PreparedDiscoveryPool> GetAsync(DiscoveryPoolRequest request, CancellationToken ct)
    {
        string hash = KeyFor(request);
        string key = "pool:" + hash;
        if (caches.Prepared.TryGetValue<PreparedDiscoveryPool>(key, out var hit) && hit!.ExpiresAtUtc > clock.GetUtcNow()) return hit;
        var gate = stripes[Convert.ToInt32(hash[..2], 16) % stripes.Length];
        await gate.WaitAsync(ct);
        try
        {
            if (caches.Prepared.TryGetValue<PreparedDiscoveryPool>(key, out hit) && hit!.ExpiresAtUtc > clock.GetUtcNow()) return hit;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(45));
            await using var scope = scopes.CreateAsyncScope();
            var provider = scope.ServiceProvider.GetRequiredService<IRecommendationProviderResolver>().Resolve();
            IngestionResult<RawCandidateTrack> raw;
            if (request.IsRecording && request.ArtistId is { } recording)
            {
                raw = provider.Source is IRecordingRecommendationSource source
                    ? await source.GetCandidatesByRecordingsAsync([recording], configuration.DiscoveryLimit, deadline.Token)
                    : new([], [new(recording.ToString("D"), "recording.similar", "direct_recording_similarity_not_supported")]);
            }
            else if (request.IsTag) raw = await provider.Source.GetCandidatesByTagsAsync([request.Seed], configuration.DiscoveryLimit, deadline.Token);
            else if (request.ArtistId is { } id)
            {
                if (provider.Source is not ICanonicalRecommendationSource canonical)
                    return new(hash, provider.Id, configuration.Origin, clock.GetUtcNow(), null, CatalogText.Version, [],
                        [new(id.ToString("D"), "discovery", "canonical_artist_not_supported")], 0, "");
                raw = await canonical.GetCandidatesByCanonicalArtistsAsync([id], configuration.DiscoveryLimit, deadline.Token);
            }
            else raw = await provider.Source.GetCandidatesByArtistsAsync([request.Seed], configuration.DiscoveryLimit, deadline.Token);
            if (raw.Count > configuration.MaximumCandidates || raw.Any(r => r.Origin != configuration.Origin))
                throw new GenerationStoppedException("prepared_evidence_origin_or_size_mismatch");
            var hydrated = await provider.Hydrator.HydrateCandidatesBatchAsync(raw.Items, deadline.Token);
            if (hydrated.Count > configuration.MaximumCandidates || hydrated.Any(c => c.Origin != configuration.Origin ||
                c.TagResponse.Origin != configuration.Origin || c.SupportingResponses.Any(r => r.Origin != configuration.Origin)))
                throw new GenerationStoppedException("prepared_evidence_origin_or_size_mismatch");
            byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(new
            {
                Candidates = hydrated.Select(c => new
                {
                    c.Track.TrackKey,
                    c.Track.Title,
                    c.Track.ArtistName,
                    c.Track.Mbid,
                    Weights = c.TagVector.Weights,
                    c.RawCandidate.Paths,
                    c.Tags,
                    c.TagResponse,
                    c.TagScope,
                    c.TagNormalizationVersion,
                    c.SupportingResponses,
                    c.TagMissingReason,
                    c.Gaps
                }).ToArray(),
                Gaps = raw.Gaps.Concat(hydrated.Gaps).ToArray()
            });
            if (serialized.Length > 2 * 1024 * 1024) throw new GenerationStoppedException("prepared_evidence_byte_limit");
            var now = clock.GetUtcNow();
            DateTimeOffset? expires = configuration.Origin == EvidenceOrigin.Synthetic ? now.AddMinutes(10) :
                provider.Source is IPublicEvidenceFreshness freshness ? freshness.SharedUntilUtc : null;
            var pool = new PreparedDiscoveryPool(hash, provider.Id, configuration.Origin, now, expires, CatalogText.Version,
                hydrated.Items, raw.Gaps.Concat(hydrated.Gaps).Distinct().ToArray(), serialized.Length,
                Convert.ToHexStringLower(SHA256.HashData(serialized)));
            if (expires > now) caches.Prepared.Set(key, pool, new MemoryCacheEntryOptions
            {
                Size = DiscoveryMemoryCaches.EstimateBytes(serialized.Length, key),
                AbsoluteExpirationRelativeToNow = expires - now
            });
            return pool;
        }
        finally { gate.Release(); }
    }
    public Task<PreparedDiscoveryPool> GetAsync(DiscoveryPoolRequest request, bool allowAcquisition, CancellationToken ct)
        => allowAcquisition ? GetAsync(request, ct) : Task.FromResult(TryGet(request)
            ?? throw new LinerNotes.Application.Common.Interfaces.Discovery.DiscoveryPreparationRequiredException());
    public void Dispose() { foreach (var gate in stripes) gate.Dispose(); }
}
