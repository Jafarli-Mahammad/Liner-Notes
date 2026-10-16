using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Catalog;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Discovery;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

/// <summary>Owner-safe status is available; submission remains closed until C7/C8 install the durable consumer.</summary>
public sealed class CatalogLookupJobs(DataContext db) : ICatalogLookupJobs
{
    public bool AcceptingRequests => false;
    public Task<CatalogLookupStatus> SubmitAsync(Guid profileId, string query, CatalogKind kind, string requestKey, CancellationToken ct) =>
        throw new GenerationStoppedException("catalog_lookup_worker_not_ready");
    public async Task<CatalogLookupStatus?> FindAsync(Guid profileId, Guid jobId, CancellationToken ct)
    {
        var job = await db.DiscoveryJobs.AsNoTracking().Where(j => j.ProfileId == profileId && j.Id == jobId && j.Kind == DiscoveryJobKind.Lookup)
            .Select(j => new { j.Id, j.State, j.ProgressCode, j.ErrorCode }).SingleOrDefaultAsync(ct);
        return job is null ? null : new(job.Id, job.State.ToString(), job.ProgressCode, job.ErrorCode);
    }
}
