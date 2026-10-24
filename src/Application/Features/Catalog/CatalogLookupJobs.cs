using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Discovery;

namespace LinerNotes.Application.Features.Catalog;

public sealed class CatalogLookupJobs(IDiscoveryJobStore jobs, IDiscoveryWriteCoordinator admission,
    ICatalogReader catalog, DiscoveryRuntimeOptions options, TimeProvider clock) : ICatalogLookupJobs
{
    public bool AcceptingRequests => options.RunJobs;
    public async Task<CatalogLookupStatus> SubmitAsync(Guid profileId, string query, CatalogKind kind, string requestKey, CancellationToken ct)
    {
        options.Validate();
        if (!AcceptingRequests) throw new GenerationStoppedException("catalog_lookup_worker_not_ready");
        if (!Enum.IsDefined(kind) || string.IsNullOrWhiteSpace(query) || query.Length > 128 || query.Any(char.IsControl))
            throw new ArgumentException("catalog_lookup_query_invalid");
        string hash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            Version = "catalog-lookup-intent-v1", Kind = kind, Query = query.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant()
        })));
        string payload = JsonSerializer.Serialize(new CatalogLookupPayload(query.Trim(), kind));
        var job = await admission.ExecuteAsync(65_536, token => jobs.EnqueueAsync(profileId, requestKey, hash, DiscoveryJobKind.Lookup,
            0, payload, clock.GetUtcNow(), options.QueueCapacity, TimeSpan.FromSeconds(options.LookupCooldownSeconds), token), ct);
        return (await FindAsync(profileId, job.Id, ct))!;
    }

    public async Task<CatalogLookupStatus?> FindAsync(Guid profileId, Guid jobId, CancellationToken ct)
    {
        var job = await jobs.FindAsync(profileId, jobId, clock.GetUtcNow(), ct);
        if (job is null || job.Kind != DiscoveryJobKind.Lookup) return null;
        List<CatalogItemDto>? items = null;
        string? error = job.ErrorCode;
        if (job.State == DiscoveryJobState.Completed && job.ResultJson is not null)
        {
            var result = JsonSerializer.Deserialize<CatalogLookupCompletion>(job.ResultJson)
                ?? throw new GenerationStoppedException("catalog_lookup_result_invalid");
            if (result.RecordIds.Count > 10) throw new GenerationStoppedException("catalog_lookup_result_invalid");
            items = (await catalog.GetManyAsync(result.RecordIds, clock.GetUtcNow(), ct)).ToList();
            if (items.Count != result.RecordIds.Count) error = "catalog_lookup_results_expired";
        }
        return new(job.Id, job.State.ToString(), job.ProgressCode, error, items);
    }
}
