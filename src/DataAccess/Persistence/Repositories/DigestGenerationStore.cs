using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Scoring;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class DigestGenerationStore(DataContext db, IUnitOfWork unitOfWork) : IDigestGenerationStore
{
    // Session lease covers the read-only acquisition phase as well as persistence, across local hosts.
    public async Task<IAsyncDisposable> AcquireBatchAsync(CancellationToken cancellationToken = default)
    {
        bool opened = db.Database.GetDbConnection().State != ConnectionState.Open;
        if (opened) await db.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(1280200758)", cancellationToken).ConfigureAwait(false);
            return new BatchLease(db, opened);
        }
        catch
        {
            if (opened) await db.Database.CloseConnectionAsync().ConfigureAwait(false);
            throw;
        }
    }

    public Task<WeeklyDigest?> GetExistingAsync(Guid userId, IsoWeek week, CancellationToken cancellationToken = default) =>
        db.WeeklyDigests.IgnoreQueryFilters().Include(d => d.Recommendations).ThenInclude(r => r.Track)
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Week == week && db.Users.Any(u => u.Id == userId), cancellationToken);

    public async Task<GenerationInputs> ReadInputsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        bool exists = await db.Users.AnyAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        var signals = await db.TasteSignals.Where(s => s.UserId == userId).OrderBy(s => s.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var feedback = await db.WeeklyRecommendations.Include(r => r.Track)
            .Where(r => r.UserId == userId && r.FeedbackGivenAt != null).OrderBy(r => r.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var revision = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Exists = exists,
            Signals = signals.Select(s => new { s.Id, s.TargetType, s.TargetValue, s.Weight, s.Source, s.Context, s.LastModifiedAt }),
            Feedback = feedback.Select(r => new { r.Id, r.Feedback, r.Rating, r.FeedbackGivenAt, r.LastModifiedAt,
                r.Track.TrackKey, r.Track.NormalizedArtistName, r.Track.NormalizedTitle, r.ScoreBreakdown.MatchedTags })
        })));
        return new(exists, signals, feedback, revision);
    }

    public async Task<GenerationStorageState> ReadStorageAsync(CancellationToken cancellationToken = default)
    {
        // Conservatively count the whole database and uncompressed rows, including TOAST/compression effects.
        var result = await db.Database.SqlQueryRaw<StorageRow>("""
            SELECT GREATEST(pg_database_size(current_database()), COALESCE(SUM(octet_length(payload)), 0))::bigint AS "DatabaseBytes",
                md5(COALESCE(string_agg(payload, '' ORDER BY kind, id), '')) AS "Revision"
            FROM (
                SELECT 'track' AS kind, "Id"::text AS id, to_jsonb(t)::text AS payload FROM "Tracks" t
                UNION ALL SELECT 'signal', "Id"::text, to_jsonb(t)::text FROM "TasteSignals" t
                UNION ALL SELECT 'digest', "Id"::text, to_jsonb(t)::text FROM "WeeklyDigests" t
                UNION ALL SELECT 'pick', "Id"::text, to_jsonb(t)::text FROM "WeeklyRecommendations" t
            ) data
            """).SingleAsync(cancellationToken).ConfigureAwait(false);
        return new(result.DatabaseBytes, result.Revision);
    }

    public Task<WeeklyDigest> PersistAsync(Guid userId, IsoWeek week, string inputRevision,
        IReadOnlyList<BaselineAPick> picks, IGenerationStorageReservation reservation,
        CancellationToken cancellationToken = default) => unitOfWork.ExecuteInTransactionAsync(async ct =>
    {
        bool leased = await db.Database.SqlQueryRaw<bool>("""
            SELECT EXISTS (SELECT 1 FROM pg_locks WHERE locktype = 'advisory' AND pid = pg_backend_pid()
                AND classid = 0 AND objid = 1280200758 AND objsubid = 1 AND granted) AS "Value"
            """).SingleAsync(ct).ConfigureAwait(false);
        if (!leased) throw new GenerationStoppedException("batch_lease_lost");
        // Brief write phase: prevents feedback/account/catalog mutations between revision check and commit.
        await db.Database.ExecuteSqlRawAsync("""
            LOCK TABLE "Users", "TasteSignals", "WeeklyRecommendations", "WeeklyDigests", "Tracks"
            IN SHARE ROW EXCLUSIVE MODE
            """, ct).ConfigureAwait(false);
        var existing = await GetExistingAsync(userId, week, ct).ConfigureAwait(false);
        if (existing is not null) return existing;
        var current = await ReadInputsAsync(userId, ct).ConfigureAwait(false);
        if (!current.UserExists || current.Revision != inputRevision) throw new GenerationStoppedException("taste_revision_changed");
        await reservation.ValidateAsync(await ReadStorageAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false);
        var monday = DateTime.SpecifyKind(System.Globalization.ISOWeek.ToDateTime(week.Year, week.WeekNumber, DayOfWeek.Monday), DateTimeKind.Utc);
        var digest = WeeklyDigest.Create(userId, week, monday, monday.AddDays(7));
        foreach (var pick in picks)
        {
            var candidate = pick.Track;
            var track = await db.Tracks.AsTracking().Where(t =>
                candidate.Mbid != null && t.Mbid != null && t.Mbid.ToLower() == candidate.Mbid.ToLower() ||
                t.NormalizedArtistName == candidate.NormalizedArtistName && t.NormalizedTitle == candidate.NormalizedTitle)
                .OrderBy(t => t.Id).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (track is null) { track = candidate; db.Tracks.Add(track); }
            digest.AddRecommendation(track, pick.Breakdown, digest.Recommendations.Count + 1);
        }
        db.WeeklyDigests.Add(digest);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        long bytes = await db.Database.SqlQuery<long>($"""
            SELECT COALESCE(SUM(pg_column_size("ScoreBreakdown")), 0)::bigint AS "Value"
            FROM "WeeklyRecommendations" WHERE "WeeklyDigestId" = {digest.Id}
            """).SingleAsync(ct).ConfigureAwait(false);
        await reservation.VerifyStoredAsync(bytes, await ReadStorageAsync(ct).ConfigureAwait(false), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return digest;
    }, cancellationToken);

    private sealed class BatchLease(DataContext context, bool opened) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try { await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(1280200758)", CancellationToken.None).ConfigureAwait(false); }
            finally { if (opened) await context.Database.CloseConnectionAsync().ConfigureAwait(false); }
        }
    }

    private sealed class StorageRow
    {
        public long DatabaseBytes { get; set; }
        public string Revision { get; set; } = "";
    }
}
