using System.Data;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LinerNotes.DataAccess.Persistence.Repositories;

/// <summary>Local PostgreSQL only. No provider/client/cache capable of outbound HTTP is injected.</summary>
public sealed class CatalogReader(DataContext db) : ICatalogReader
{
    private sealed record Hit(Guid RecordId, double Distance);

    public async Task<CatalogSearchResult> SearchAsync(string query, CatalogKind kind, int limit, int offset, DateTimeOffset now, CancellationToken ct)
    {
        if (query.Length > 128 || !Enum.IsDefined(kind) || limit is < 1 or > 10 || offset is < 0 or > 40)
            throw new ArgumentException("catalog_search_bounds");
        var normalized = CatalogText.Normalize(query);
        if (normalized.Length == 0) return new([], false, CatalogText.Version);
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
                await db.Database.ExecuteSqlRawAsync("SET LOCAL statement_timeout = '250ms'", ct);
                var parameters = new object[] { new NpgsqlParameter("query", normalized), new NpgsqlParameter("prefix", normalized.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%"),
                    new NpgsqlParameter("kind", kind.ToString()), new NpgsqlParameter("now", now) };
                const string eligibility = """
                    p."State"='Published' AND (p."ExpiresAtUtc" IS NULL OR p."ExpiresAtUtc">@now)
                    AND (p."IsLookup" OR EXISTS (SELECT 1 FROM "CatalogHead" h WHERE h."PublicationId"=p."Id"))
                    """;
                var prefix = await db.Database.SqlQueryRaw<Hit>("""
                    SELECT candidate."RecordId", min(candidate."Distance") AS "Distance" FROM (
                        SELECT t."RecordId", CASE WHEN t."NormalizedTerm"=@query THEN 0.0 ELSE 0.05 END::float8 AS "Distance"
                        FROM "CatalogSearchTerms" t JOIN "CatalogRecords" r ON r."Id"=t."RecordId"
                        JOIN "CatalogPublications" p ON p."Id"=r."PublicationId"
                        WHERE t."Kind"=@kind AND t."NormalizedTerm" LIKE @prefix ESCAPE E'\\' AND
                    """ + "\n" + eligibility + "\n" + """
                        ORDER BY t."NormalizedTerm",t."Kind",t."RecordId" LIMIT 240
                    ) candidate GROUP BY candidate."RecordId" ORDER BY "Distance",candidate."RecordId" LIMIT 120
                    """, parameters).ToArrayAsync(ct);
                var hits = prefix.ToList();
                if (normalized.Length >= 4 && hits.Count <= offset + limit)
                {
                    var fuzzy = await db.Database.SqlQueryRaw<Hit>("""
                        SELECT candidate."RecordId", min(candidate."Distance") AS "Distance" FROM (
                            SELECT t."RecordId", (t."NormalizedTerm" <-> @query)::float8 AS "Distance"
                            FROM "CatalogSearchTerms" t JOIN "CatalogRecords" r ON r."Id"=t."RecordId"
                            JOIN "CatalogPublications" p ON p."Id"=r."PublicationId"
                            WHERE t."Kind"=@kind AND
                        """ + "\n" + eligibility + "\n" + """
                            ORDER BY t."NormalizedTerm" <-> @query LIMIT 120
                        ) candidate WHERE candidate."Distance"<=0.65
                        GROUP BY candidate."RecordId" ORDER BY "Distance",candidate."RecordId"
                        """, new NpgsqlParameter("query", normalized), new NpgsqlParameter("kind", kind.ToString()), new NpgsqlParameter("now", now)).ToArrayAsync(ct);
                    hits.AddRange(fuzzy.Select(h => h with { Distance = Math.Max(0.15, h.Distance) }));
                }
                var ordered = hits.GroupBy(h => h.RecordId).Select(g => g.MinBy(h => h.Distance)!)
                    .OrderBy(h => h.Distance).ThenBy(h => h.RecordId).Skip(offset).Take(limit + 1).ToArray();
                var ids = ordered.Take(limit).Select(h => h.RecordId).ToArray();
                var records = await ReadItemsAsync(ids, ct);
                await transaction.CommitAsync(ct);
                return new CatalogSearchResult(ids.Select(id => records.Single(r => r.Id == id)).ToArray(), ordered.Length > limit, CatalogText.Version);
            });
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.QueryCanceled)
        { throw new GenerationStoppedException("catalog_search_busy"); }
    }

    public async Task<CatalogItemDto?> GetAsync(Guid recordId, DateTimeOffset now, CancellationToken ct)
    {
        var eligible = await (from r in db.CatalogRecords.AsNoTracking()
                              join p in db.CatalogPublications.AsNoTracking() on r.PublicationId equals p.Id
                              where r.Id == recordId && p.State == CatalogPublicationState.Published && (p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)
                              select r.Id).AnyAsync(ct);
        return eligible ? (await ReadItemsAsync([recordId], ct)).SingleOrDefault() : null;
    }

    private async Task<CatalogItemDto[]> ReadItemsAsync(Guid[] ids, CancellationToken ct)
    {
        var rows = await (from r in db.CatalogRecords.AsNoTracking()
                          join p in db.CatalogPublications.AsNoTracking() on r.PublicationId equals p.Id
                          where ids.Contains(r.Id)
                          select new { Record = r, Publication = p }).ToArrayAsync(ct);
        return rows.Select(row => new CatalogItemDto(row.Record.Id, row.Record.CanonicalId, row.Record.Kind, row.Record.DisplayName,
            row.Record.ArtistDisplayName, row.Record.Disambiguation, row.Record.FirstReleaseDate,
            (JsonSerializer.Deserialize<CatalogTagInput[]>(row.Record.TagsJson) ?? []).Select(t => t.Name).ToArray(),
            row.Publication.Source, row.Publication.Attribution, row.Publication.Id, row.Publication.Origin,
            JsonSerializer.Deserialize<CatalogArtistCreditDto[]>(row.Record.CreditsJson) ?? [], row.Publication.MetadataLicense, row.Publication.TagLicense,
            JsonSerializer.Deserialize<CatalogTagInput[]>(row.Record.TagsJson) ?? [], row.Publication.AcquiredAtUtc,
            row.Publication.ExpiresAtUtc, row.Publication.ContentSha256, row.Publication.NormalizationVersion, JsonSerializer.Deserialize<CatalogReleaseDto[]>(row.Record.ReleasesJson) ?? [])).ToArray();
    }
}
