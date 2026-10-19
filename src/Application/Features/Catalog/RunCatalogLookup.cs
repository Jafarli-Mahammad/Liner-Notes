using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Discovery;

namespace LinerNotes.Application.Features.Catalog;

public sealed record CatalogLookupPayload(string Query, CatalogKind Kind);
public sealed record CatalogLookupCompletion(Guid? PublicationId, IReadOnlyList<Guid> RecordIds);

public sealed class RunCatalogLookup(ICatalogLookupPreparation preparation, ImportCatalog import,
    IDiscoveryJobStore jobs, IDiscoveryWriteCoordinator admission, TimeProvider clock)
{
    private static readonly JsonSerializerOptions ArtifactJson = new() {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task ExecuteAsync(DiscoveryJob job, CancellationToken ct)
    {
        if (job.Kind != DiscoveryJobKind.Lookup || job.LeaseToken is not { } lease)
            throw new InvalidOperationException("lookup_lease_required");
        var request = JsonSerializer.Deserialize<CatalogLookupPayload>(job.PayloadJson)
            ?? throw new GenerationStoppedException("job_payload_invalid");
        var result = await preparation.SearchAsync(request.Query, request.Kind, ct);
        if (result.CacheUntilUtc is not { } expiry || expiry <= clock.GetUtcNow())
            throw new GenerationStoppedException("catalog_lookup_not_retainable");
        if (result.Records.Count > 250 || result.MatchedCanonicalIds.Count > 10)
            throw new GenerationStoppedException("catalog_lookup_size_limit");
        Guid? publicationId = null;
        Guid[] ids = [];
        if (result.Records.Count > 0)
        {
            await using var artifact = new MemoryStream();
            foreach (var row in result.Records.OrderBy(r => r.Kind).ThenBy(r => r.Id))
            {
                byte[] line = JsonSerializer.SerializeToUtf8Bytes(row, ArtifactJson);
                if (artifact.Length + line.Length + 1 > 2_097_152) throw new GenerationStoppedException("catalog_lookup_size_limit");
                await artifact.WriteAsync(line, ct); artifact.WriteByte((byte)'\n');
            }
            artifact.Position = 0;
            string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(artifact, ct));
            static DateTimeOffset Microseconds(DateTimeOffset value) => new(value.Ticks - value.Ticks % 10, TimeSpan.Zero);
            var acquired = Microseconds(result.AcquiredAtUtc);
            var expires = Microseconds(expiry);
            var identity = JsonSerializer.SerializeToUtf8Bytes(new { Version = "lookup-publication-v1", hash, result.Source, result.SourceUri,
                result.ResponseSha256, result.Origin, Acquired = acquired, Expires = expires });
            publicationId = new Guid(SHA256.HashData(identity).AsSpan(0, 16));
            bool synthetic = result.Origin == CatalogOrigin.Synthetic;
            var manifest = new CatalogPublication(publicationId.Value, result.Source, result.ResponseSha256, result.SourceUri, result.Origin,
                synthetic ? "synthetic-fixture" : "CC0-1.0", null, synthetic ? "Synthetic fixture" : "MusicBrainz (CC0)",
                hash, artifact.Length, result.Records.Count, acquired, expires, isLookup: true);
            artifact.Position = 0;
            await import.RunAsync(manifest, artifact, exactManifestApproved: true, supplementaryUseApproved: false, ct);
            ids = result.MatchedCanonicalIds.Select(id => CatalogText.RecordId(publicationId.Value, request.Kind, id)).ToArray();
        }
        string completion = JsonSerializer.Serialize(new CatalogLookupCompletion(publicationId, ids));
        await admission.ExecuteAsync(65_536, async token => {
            if (expiry <= clock.GetUtcNow()) throw new GenerationStoppedException("catalog_lookup_evidence_expired");
            var current = await jobs.LockLeasedAsync(job.Id, lease, clock.GetUtcNow(), token);
            await jobs.SaveCompletedAsync(current, lease, clock.GetUtcNow(), null, completion, token);
            return true;
        }, ct);
    }
}
