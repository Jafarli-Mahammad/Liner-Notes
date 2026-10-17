using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage;
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
        var profileId = await db.ListenerProfiles.Where(p => p.UserId == userId && db.Users.Any(u => u.Id == userId))
            .Select(p => (Guid?)p.Id).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        bool exists = profileId.HasValue;
        var signals = await db.TasteSignals.Where(s => db.ListenerProfiles.Any(p => p.Id == s.ProfileId && p.UserId == userId)).OrderBy(s => s.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var feedback = await db.WeeklyRecommendations.Include(r => r.Track)
            .Where(r => db.ListenerProfiles.Any(p => p.Id == r.ProfileId && p.UserId == userId) && r.FeedbackGivenAt != null).OrderBy(r => r.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var revision = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Exists = exists,
            ProfileId = profileId,
            Signals = signals.Select(s => new { s.Id, s.TargetType, s.TargetValue, s.Weight, s.Source, s.Context, s.LastModifiedAt }),
            Feedback = feedback.Select(r => new
            {
                r.Id,
                r.Feedback,
                r.Rating,
                r.FeedbackGivenAt,
                r.LastModifiedAt,
                r.Track.TrackKey,
                r.Track.NormalizedArtistName,
                r.Track.NormalizedTitle,
                r.ScoreBreakdown.MatchedTags
            })
        })));
        return new(exists, signals, feedback, revision, profileId);
    }

    public async Task<GenerationStorageState> ReadStorageAsync(CancellationToken cancellationToken = default)
    {
        var connection = db.Database.GetDbConnection();
        bool opened = connection.State != ConnectionState.Open;
        if (opened) await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT pg_database_size(current_database()),l."Revision",l."LogicalBytes",l."CoreRevision",l."CoreLogicalBytes",
                  (SELECT md5(string_agg(n.nspname||'.'||c.relname||':'||c.oid::text||':'||c.relfilenode::text||':'||
                     COALESCE((SELECT string_agg(a.attname||':'||a.atttypid::text||':'||a.attnotnull::text,',' ORDER BY a.attnum) FROM pg_attribute a WHERE a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped),'')||':'||
                     COALESCE((SELECT string_agg(t.tgname||':'||t.tgenabled::text,',' ORDER BY t.tgname) FROM pg_trigger t WHERE t.tgrelid=c.oid AND NOT t.tgisinternal),''),',' ORDER BY n.nspname,c.relname))
                   FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE c.relkind IN ('r','i','S','v') AND n.nspname IN ('public','identity'))
                FROM "ManagedStorageLedger" l WHERE l."Id"=1 AND current_setting('session_replication_role')='origin'
                  AND NOT EXISTS (SELECT 1 FROM pg_tables t WHERE t.schemaname IN ('public','identity')
                    AND t.tablename NOT IN ('__EFMigrationsHistory','ManagedStorageLedger')
                    AND (NOT EXISTS (SELECT 1 FROM pg_trigger g WHERE g.tgrelid=(quote_ident(t.schemaname)||'.'||quote_ident(t.tablename))::regclass
                      AND g.tgname='liner_storage_delta' AND g.tgenabled='O') OR
                      NOT EXISTS (SELECT 1 FROM pg_trigger g WHERE g.tgrelid=(quote_ident(t.schemaname)||'.'||quote_ident(t.tablename))::regclass
                      AND g.tgname='liner_storage_truncate' AND g.tgenabled='O')))
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new GenerationStoppedException("managed_storage_ledger_missing_or_schema_uncovered");
            long size = reader.GetInt64(0), revision = reader.GetInt64(1), logical = reader.GetInt64(2);
            return new(Math.Max(size, logical), $"managed-v1:{reader.GetInt64(3)}:{reader.GetInt64(4)}:{revision}:{logical}:{reader.GetString(5)}");
        }
        finally { if (opened) await db.Database.CloseConnectionAsync(); }
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
        var profileId = await db.ListenerProfiles.Where(p => p.UserId == userId).Select(p => p.Id).SingleAsync(ct);
        var digest = new WeeklyDigest(userId, week, monday, monday.AddDays(7), profileId: profileId);
        foreach (var pick in picks)
        {
            var candidate = pick.Track;
            var track = await db.Tracks.AsTracking().Where(t =>
                t.NormalizedArtistName == candidate.NormalizedArtistName && t.NormalizedTitle == candidate.NormalizedTitle &&
                (candidate.Mbid == null ? t.Mbid == null : t.Mbid != null && t.Mbid.ToLower() == candidate.Mbid.ToLower()))
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

}
