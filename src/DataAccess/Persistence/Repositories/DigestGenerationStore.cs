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
        var connection = db.Database.GetDbConnection();
        bool opened = connection.State != ConnectionState.Open;
        if (opened) await db.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var consistencyTransaction = db.Database.CurrentTransaction is null
                ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false)
                : null;
            var currentTransaction = db.Database.CurrentTransaction?.GetDbTransaction();
            await using var sizeCommand = connection.CreateCommand();
            sizeCommand.Transaction = currentTransaction;
            sizeCommand.CommandText = "SELECT pg_database_size(current_database())";
            long databaseBytes = Convert.ToInt64(await sizeCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
                System.Globalization.CultureInfo.InvariantCulture);

            using var revision = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            using var rowHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long payloadBytes = 0;
            foreach (var (kind, table) in new[]
                { ("track", "Tracks"), ("signal", "TasteSignals"), ("digest", "WeeklyDigests"), ("pick", "WeeklyRecommendations") })
            {
                revision.AppendData(Encoding.UTF8.GetBytes(kind));
                revision.AppendData([0]);
                await using var rowsCommand = connection.CreateCommand();
                rowsCommand.Transaction = currentTransaction;
                rowsCommand.CommandText = $"SELECT \"Id\"::text, to_jsonb(t)::text FROM \"{table}\" t ORDER BY \"Id\"";
                await using var reader = await rowsCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    string id = reader.GetString(0);
                    string payload = reader.GetString(1);
                    rowHash.AppendData(Encoding.UTF8.GetBytes(id));
                    rowHash.AppendData([0]);
                    rowHash.AppendData(Encoding.UTF8.GetBytes(payload));
                    revision.AppendData(rowHash.GetHashAndReset());
                    payloadBytes = checked(payloadBytes + Encoding.UTF8.GetByteCount(payload));
                }
            }
            var result = new GenerationStorageState(Math.Max(databaseBytes, payloadBytes),
                Convert.ToHexStringLower(revision.GetHashAndReset()));
            if (consistencyTransaction is not null)
                await consistencyTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        finally
        {
            if (opened) await db.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
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
