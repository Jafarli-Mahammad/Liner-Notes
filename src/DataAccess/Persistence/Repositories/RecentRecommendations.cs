using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Discovery;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class RecentRecommendations(DataContext db) : IRecentRecommendations
{
    public async Task<RecentMusicPage> ReadAsync(long? before, int limit, DateTimeOffset now, CancellationToken ct)
    {
        if (before <= 0 || limit is < 1 or > 50) throw new ArgumentException("invalid_recent_page");
        var rows = await db.Set<RecentRecommendation>().AsNoTracking()
            .Where(r => (before == null || r.Sequence < before) && (r.MetadataExpiresAtUtc == null || r.MetadataExpiresAtUtc > now) &&
                db.CatalogRecords.Any(c => c.CanonicalId == r.RecordingId && c.Kind == LinerNotes.Domain.Catalog.CatalogKind.Track &&
                    db.CatalogPublications.Any(p => p.Id == c.PublicationId && p.Origin == r.Origin &&
                        p.State == LinerNotes.Domain.Catalog.CatalogPublicationState.Published &&
                        (p.Origin == LinerNotes.Domain.Catalog.CatalogOrigin.Synthetic || p.MetadataLicense == "CC0-1.0") && (p.ExpiresAtUtc == null || p.ExpiresAtUtc > now) &&
                        (p.IsLookup || db.CatalogHeads.Any(h => h.PublicationId == p.Id)))))
            .OrderByDescending(r => r.Sequence).Take(limit + 1)
            .Select(r => new RecentMusic(r.Sequence, r.RecordingId, r.Title, r.ArtistName, r.Origin.ToString(), r.Attribution)).ToArrayAsync(ct);
        return new(rows.Take(limit).ToArray(), rows.Length > limit ? rows[limit - 1].Sequence : null);
    }
}
