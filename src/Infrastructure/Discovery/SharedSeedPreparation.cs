using System.Security.Cryptography;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
using LinerNotes.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace LinerNotes.Infrastructure.Discovery;

public sealed class SharedSeedPreparation(IServiceScopeFactory scopes, SharedPublicEvidence evidence,
    DiscoveryMemoryCaches caches, GenerationConfiguration configuration, TimeProvider clock) : IDiscoverySeedPreparation
{
    public async Task<SeedTagSnapshot> GetAsync(DiscoverySeedRequest request, bool allowAcquisition, CancellationToken ct)
    {
        configuration.Scoring();
        if (configuration.Origin == EvidenceOrigin.Recorded) throw new GenerationStoppedException("evaluation_evidence_ineligible");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200 || request.Name.Any(char.IsControl) ||
            request.EntityId == Guid.Empty || (request.IsAlbum && request.EntityId is null))
            throw new ArgumentException("invalid_seed_request");
        string key = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            Version = PublicEvidenceCodec.SeedVersion, Configuration = configuration, request.EntityId, request.IsAlbum,
            Name = request.EntityId.HasValue ? null : request.Name.Trim().ToLowerInvariant()
        })));
        string memoryKey = "seed:" + key;
        if (caches.Prepared.TryGetValue<PublicEvidenceDocument>(memoryKey, out var hit) && hit!.ExpiresAtUtc > clock.GetUtcNow())
            return PublicEvidenceCodec.DecodeSeed(hit);
        var document = await evidence.GetAsync(key, PublicEvidenceKind.Seed, PublicEvidenceCodec.SeedVersion,
            allowAcquisition, async token => {
                await using var scope = scopes.CreateAsyncScope();
                var provider = scope.ServiceProvider.GetRequiredService<IRecommendationProviderResolver>().Resolve();
                SeedTagSnapshot seed;
                if (request.EntityId is { } id)
                {
                    if (provider.SeedTags is ICanonicalSeedTagSource canonical)
                        seed = request.IsAlbum ? await canonical.GetAlbumAsync(id, request.Name, token)
                            : await canonical.GetArtistAsync(id, request.Name, token);
                    else seed = new(request.Name, WeightedTagVector.Empty, [],
                        new(provider.Id, "seed.tags", id.ToString("D"), configuration.Origin, null, null),
                        "canonical_seed_tags_not_supported", []);
                }
                else seed = await provider.SeedTags.GetAsync(request.Name, token);
                var now = clock.GetUtcNow();
                DateTimeOffset? expiry = configuration.Origin == EvidenceOrigin.Synthetic ? now.AddMinutes(10)
                    : provider.Source is IPublicEvidenceFreshness freshness ? freshness.SharedUntilUtc : null;
                return PublicEvidenceCodec.EncodeSeed(key, provider.Id, seed, now, expiry);
            }, ct);
        var remaining = document.ExpiresAtUtc - clock.GetUtcNow();
        if (remaining > TimeSpan.Zero)
            caches.Prepared.Set(memoryKey, document, new MemoryCacheEntryOptions {
                Size = DiscoveryMemoryCaches.EstimateBytes(System.Text.Encoding.UTF8.GetByteCount(document.PayloadJson), memoryKey),
                AbsoluteExpirationRelativeToNow = remaining
            });
        return PublicEvidenceCodec.DecodeSeed(document);
    }
}
