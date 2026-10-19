using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.Domain.Catalog;
using Microsoft.Extensions.DependencyInjection;

namespace LinerNotes.Infrastructure.Discovery;

public sealed class SharedCatalogLookup(IServiceScopeFactory scopes, SharedPublicEvidence evidence,
    GenerationConfiguration generation) : ICatalogLookupPreparation
{
    public async Task<CatalogLookupResult> SearchAsync(string query, CatalogKind kind, CancellationToken ct)
    {
        if (!Enum.IsDefined(kind) || string.IsNullOrWhiteSpace(query) || query.Length > 128 || query.Any(char.IsControl))
            throw new ArgumentException("catalog_lookup_query_invalid");
        string normalized = query.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        string key = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            Version = "public-catalog-lookup-v1", Configuration = generation, Kind = kind, Query = normalized
        })));
        var document = await evidence.GetAsync(key, PublicEvidenceKind.Lookup, "catalog-lookup-v1", true, async token => {
            await using var scope = scopes.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ICatalogLookupSource>().SearchAsync(query.Trim(), kind, token);
            if (result.Records.Count > 250 || result.MatchedCanonicalIds.Count > 10)
                throw new GenerationStoppedException("catalog_lookup_size_limit");
            return PublicEvidenceCodec.Document(key, PublicEvidenceKind.Lookup, generation.Provider,
                result.Origin == CatalogOrigin.Synthetic ? EvidenceOrigin.Synthetic : EvidenceOrigin.Live,
                "catalog-lookup-v1", result, result.AcquiredAtUtc, result.CacheUntilUtc ?? result.AcquiredAtUtc);
        }, ct);
        return JsonSerializer.Deserialize<CatalogLookupResult>(document.PayloadJson)
            ?? throw new GenerationStoppedException("catalog_lookup_evidence_invalid");
    }
}
