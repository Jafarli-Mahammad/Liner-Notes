using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class ArtworkCatalog(DataContext db) : IArtworkCatalog
{
    public async Task<IReadOnlyList<Guid>> PreviouslyPublicAsync(IReadOnlyList<Guid> recordingIds, CancellationToken ct)
    {
        if (recordingIds.Count > 50) throw new ArgumentException("artwork_batch_limit");
        return await db.CatalogRecords.AsNoTracking().Where(r => r.Kind == CatalogKind.Track && recordingIds.Contains(r.CanonicalId) &&
            db.CatalogPublications.Any(p => p.Id == r.PublicationId && p.Origin == CatalogOrigin.Public &&
                p.State == CatalogPublicationState.Published && p.MetadataLicense == "CC0-1.0" &&
                (p.IsLookup || db.CatalogHeads.Any(h => h.PublicationId == p.Id))))
            .Select(r => r.CanonicalId).Distinct().ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> EligibleAsync(IReadOnlyList<Guid> recordingIds, DateTimeOffset now, CancellationToken ct)
    {
        if (recordingIds.Count > 50) throw new ArgumentException("artwork_batch_limit");
        return await db.CatalogRecords.AsNoTracking().Where(r => r.Kind == CatalogKind.Track && recordingIds.Contains(r.CanonicalId) &&
            db.CatalogPublications.Any(p => p.Id == r.PublicationId && p.Origin == CatalogOrigin.Public &&
                p.State == CatalogPublicationState.Published && p.MetadataLicense == "CC0-1.0" &&
                (p.ExpiresAtUtc == null || p.ExpiresAtUtc > now) &&
                (p.IsLookup || db.CatalogHeads.Any(h => h.PublicationId == p.Id))))
            .Select(r => r.CanonicalId).Distinct().ToArrayAsync(ct);
    }
}
