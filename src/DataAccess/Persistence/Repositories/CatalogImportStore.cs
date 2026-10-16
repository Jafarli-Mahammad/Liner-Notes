using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class CatalogImportStore(DataContext db) : ICatalogImportStore
{
    public Task<CatalogPublication?> FindAsync(Guid publicationId, CancellationToken ct) =>
        db.CatalogPublications.AsNoTracking().SingleOrDefaultAsync(p => p.Id == publicationId, ct);

    public async Task StartAsync(CatalogPublication publication, CancellationToken ct)
    {
        RequireTransaction();
        if (!await db.CatalogHeads.AnyAsync(ct)) db.CatalogHeads.Add(new CatalogHead());
        db.CatalogPublications.Add(publication);
        await db.SaveChangesAsync(ct);
        DetachImportedEntities();
    }

    public async Task AppendAsync(Guid publicationId, int expectedCursor, IReadOnlyList<CatalogArtifactRow> rows, CancellationToken ct)
    {
        RequireTransaction();
        if (rows.Count is < 1 or > 250) throw new ArgumentException("catalog_chunk_bounds");
        var publication = await LockAsync(publicationId, ct);
        if (publication.State != CatalogPublicationState.Staging || publication.ImportedRecords != expectedCursor)
            throw new InvalidOperationException("catalog_import_cursor_conflict");
        var needed = rows.Where(r => r.Kind == CatalogKind.Artist).Select(r => r.Id).Concat(rows.SelectMany(r => r.ArtistIds)).Distinct().ToArray();
        var mbids = needed.Select(id => id.ToString("D")).ToArray();
        var artists = await db.Artists.AsNoTracking().Where(a => a.Mbid != null && mbids.Contains(a.Mbid.ToLower())).OrderBy(a => a.Id).ToArrayAsync(ct);
        var artistMap = artists.GroupBy(a => Guid.Parse(a.Mbid!)).ToDictionary(g => g.Key, g => g.First());
        foreach (var row in rows.Where(r => r.Kind == CatalogKind.Artist))
        {
            if (artistMap.ContainsKey(row.Id)) continue;
            var artist = new Artist(row.Name, row.Id.ToString("D"), row.Id);
            db.Artists.Add(artist); artistMap.Add(row.Id, artist);
        }
        if (rows.SelectMany(r => r.ArtistIds).Any(id => !artistMap.ContainsKey(id))) throw new ArgumentException("catalog_artist_reference_missing");
        var albumIds = rows.Where(r => r.Kind == CatalogKind.Album).Select(r => r.Id).ToArray();
        var albums = await db.Albums.AsNoTracking().Where(a => a.ReleaseGroupMbid != null && albumIds.Contains(a.ReleaseGroupMbid.Value)).ToArrayAsync(ct);
        var albumMap = albums.ToDictionary(a => a.ReleaseGroupMbid!.Value);
        foreach (var row in rows.Where(r => r.Kind == CatalogKind.Album))
        {
            if (albumMap.ContainsKey(row.Id)) continue;
            var names = row.ArtistIds.Select(id => artistMap[id].Name).ToArray();
            string name = string.Join(", ", names);
            var album = new Album(row.Name, name.Length <= 256 ? name : names[0], artistId: artistMap[row.ArtistIds[0]].Id,
                id: row.Id, releaseGroupMbid: row.Id);
            db.Albums.Add(album); albumMap.Add(row.Id, album);
            for (int i = 0; i < row.ArtistIds.Length; i++) db.AlbumCredits.Add(new AlbumCredit(album.Id, artistMap[row.ArtistIds[i]].Id, i));
        }
        var labels = await db.CatalogRecords.AsNoTracking().Where(r => r.PublicationId == publicationId && r.Kind == CatalogKind.Artist && needed.Contains(r.CanonicalId))
            .Select(r => new { r.CanonicalId, r.DisplayName }).ToDictionaryAsync(r => r.CanonicalId, r => r.DisplayName, ct);
        foreach (var artist in rows.Where(r => r.Kind == CatalogKind.Artist)) labels[artist.Id] = artist.Name;
        if (rows.SelectMany(r => r.ArtistIds).Any(id => !labels.ContainsKey(id)))
            throw new ArgumentException("catalog_current_publication_credit_missing");
        foreach (var row in rows)
        {
            var credits = row.ArtistIds.Select((id, position) => new CatalogArtistCreditDto(id,
                labels.GetValueOrDefault(id, artistMap[id].Name), position)).ToArray();
            string? artistName = row.Kind == CatalogKind.Album ? string.Join(", ", credits.Select(c => c.Name)) : null;
            if (artistName?.Length > 1000) artistName = credits[0].Name + $" +{row.ArtistIds.Length - 1} more artists";
            var record = new CatalogRecord(publicationId, row.Id, row.Kind,
                row.Kind == CatalogKind.Artist ? artistMap[row.Id].Id : albumMap[row.Id].Id,
                row.Name, artistName, row.Disambiguation, row.FirstReleaseDate, JsonSerializer.Serialize(row.Aliases), JsonSerializer.Serialize(row.Tags), CatalogRowFingerprint.Sha256(row), JsonSerializer.Serialize(credits));
            db.CatalogRecords.Add(record);
            foreach (var term in CatalogText.Terms(row.Name, row.Aliases)) db.CatalogSearchTerms.Add(new CatalogSearchTerm(record.Id, row.Kind, term));
        }
        publication.Advance(expectedCursor, rows.Count);
        await db.SaveChangesAsync(ct);
        DetachImportedEntities();
    }

    public async Task VerifyRowsAsync(Guid publicationId, IReadOnlyList<CatalogArtifactRow> rows, CancellationToken ct)
    {
        var expected = rows.ToDictionary(r => CatalogText.RecordId(publicationId, r.Kind, r.Id), CatalogRowFingerprint.Sha256);
        var ids = expected.Keys.ToArray();
        var persisted = await db.CatalogRecords.AsNoTracking().Where(r => r.PublicationId == publicationId && ids.Contains(r.Id))
            .Select(r => new { r.Id, r.RowSha256 }).ToArrayAsync(ct);
        if (persisted.Length != rows.Count || persisted.Any(r => r.RowSha256 != expected[r.Id]))
            throw new InvalidOperationException("catalog_resume_row_integrity_mismatch");
    }

    public async Task PublishAsync(Guid publicationId, DateTimeOffset now, CancellationToken ct)
    {
        RequireTransaction();
        var publication = await LockAsync(publicationId, ct);
        publication.Publish(now);
        if (!publication.IsLookup)
        {
            var head = await db.CatalogHeads.AsTracking().SingleAsync(ct);
            head.Publish(publicationId);
        }
        await db.SaveChangesAsync(ct);
        DetachImportedEntities();
    }

    private Task<CatalogPublication> LockAsync(Guid publicationId, CancellationToken ct) =>
        db.CatalogPublications.FromSqlInterpolated($"SELECT * FROM \"CatalogPublications\" WHERE \"Id\"={publicationId} FOR UPDATE")
            .AsTracking().SingleAsync(ct);
    private void DetachImportedEntities()
    {
        // An import spans many short transactions. Do not retain the entire catalog in EF's identity map.
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.Entity is Artist or Album or AlbumCredit or
            CatalogPublication or CatalogHead or CatalogRecord or CatalogSearchTerm).ToArray())
            entry.State = EntityState.Detached;
    }
    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("catalog_write_requires_coordinated_transaction");
    }
}
