using System.Data;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using Npgsql;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class WeeklyScheduleStore(string connectionString) : IWeeklyScheduleStore
{
    private const long LeaseKey = 1280200760;
    private string ConnectionString => connectionString;
    private const string Eligible = """
        NOT u."IsDeleted" AND u."EmailUnsubscribedAtUtc" IS NULL
        AND EXISTS (SELECT 1 FROM identity."Users" i WHERE i."Id" = u."Id" AND NOT i."IsDeleted")
        """;

    /// <summary>Returns a disposable PostgreSQL scheduler lease, or null if another session holds it.</summary>
    /// <remarks>Connection and database errors and cancellation propagate. Disposing the lease releases its session lock.</remarks>
    public async Task<IScheduledTickLease?> TryAcquireTickAsync(CancellationToken ct)
    {
        var settings = new NpgsqlConnectionStringBuilder(ConnectionString) { Pooling = false };
        var connection = new NpgsqlConnection(settings.ConnectionString);
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);
            command.Parameters.AddWithValue("key", LeaseKey);
            if ((bool)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!) return new TickLease(connection);
            await connection.DisposeAsync().ConfigureAwait(false);
            return null;
        }
        catch { await connection.DisposeAsync().ConfigureAwait(false); throw; }
    }

    /// <summary>Reads eligible allowlisted accounts due at or before <paramref name="nowUtc"/>, oldest first, followed by accounts with no slot.</summary>
    /// <remarks>Deleted or unsubscribed accounts and missing or deleted Identity accounts are excluded; ties use user ID. Database errors and cancellation propagate.</remarks>
    /// <exception cref="ArgumentException">The allowlist count or result limit is outside 1–100.</exception>
    public async Task<IReadOnlyList<ScheduledUser>> GetCandidatesAsync(IReadOnlyList<Guid> users, int limit, DateTime nowUtc, CancellationToken ct)
    {
        if (users.Count is < 1 or > 100 || limit is < 1 or > 100) throw new ArgumentException("invalid_schedule_request");
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"""
            SELECT u."Id", u."DeliveryDay", u."DeliveryHourUtc", u."NextDigestAt"
            FROM "Users" u WHERE {Eligible} AND u."Id" = ANY(@users)
            AND (u."NextDigestAt" IS NULL OR u."NextDigestAt" <= @now)
            ORDER BY (u."NextDigestAt" IS NULL), u."NextDigestAt", u."Id" LIMIT @limit
            """, connection);
        command.Parameters.AddWithValue("users", users.ToArray());
        command.Parameters.AddWithValue("now", nowUtc);
        command.Parameters.AddWithValue("limit", limit);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        var result = new List<ScheduledUser>();
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) result.Add(Read(reader));
        return result;
    }

    /// <summary>Returns the raw stored schedule, or null for a missing, deleted, or unsubscribed subscriber or missing or deleted Identity account.</summary>
    /// <remarks>Database errors and cancellation propagate.</remarks>
    public async Task<ScheduledUser?> GetAsync(Guid id, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = new NpgsqlCommand($"""
            SELECT u."Id", u."DeliveryDay", u."DeliveryHourUtc", u."NextDigestAt"
            FROM "Users" u WHERE {Eligible} AND u."Id" = @id
            """, connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? Read(reader) : null;
    }

    /// <summary>Reads a schedule row while preserving the stored day text and a missing due slot.</summary>
    private static ScheduledUser Read(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1),
        reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetDateTime(3));

    /// <summary>Sets a null due slot to <paramref name="nextUtc"/> only while eligibility and observed preferences still match.</summary>
    /// <returns>True if one row and its modification metadata were updated; false on a conflict or ineligible account.</returns>
    /// <remarks>The modification time is <paramref name="nowUtc"/>. Slot validity is the caller's responsibility; database errors and cancellation propagate.</remarks>
    public Task<bool> InitializeAsync(ScheduledUser observed, DateTime nextUtc, DateTime nowUtc, CancellationToken ct) =>
        UpdateAsync(observed, nextUtc, nowUtc, initialize: true, ct);

    /// <summary>Replaces the observed due slot with <paramref name="nextUtc"/> only while eligibility, preferences, and due slot still match.</summary>
    /// <returns>True if one row and its modification metadata were updated; false on a conflict or ineligible account.</returns>
    /// <remarks>The modification time is <paramref name="nowUtc"/>. Slot validity is the caller's responsibility; database errors and cancellation propagate.</remarks>
    /// <exception cref="ArgumentException">The observed due slot is null.</exception>
    public Task<bool> AdvanceAsync(ScheduledUser observed, DateTime nextUtc, DateTime nowUtc, CancellationToken ct) =>
        UpdateAsync(observed, nextUtc, nowUtc, initialize: false, ct);

    /// <summary>Conditionally updates the due slot and modification metadata, returning whether one eligible row matched.</summary>
    /// <remarks>When <paramref name="initialize"/> is true, requires a null stored slot; otherwise requires the observed non-null slot. Database errors and cancellation propagate; a missing observed slot for advancement raises <see cref="ArgumentException"/>.</remarks>
    private async Task<bool> UpdateAsync(ScheduledUser observed, DateTime nextUtc, DateTime nowUtc, bool initialize, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);
        var duePredicate = initialize ? "u.\"NextDigestAt\" IS NULL" : "u.\"NextDigestAt\" = @due";
        await using var command = new NpgsqlCommand($"""
            UPDATE "Users" u SET "NextDigestAt" = @next, "LastModifiedAt" = @now, "LastModifiedBy" = NULL
            WHERE {Eligible} AND u."Id" = @id AND u."DeliveryDay" = @day
            AND u."DeliveryHourUtc" = @hour AND {duePredicate}
            """, connection);
        command.Parameters.AddWithValue("id", observed.Id);
        command.Parameters.AddWithValue("day", observed.DeliveryDay);
        command.Parameters.AddWithValue("hour", observed.DeliveryHourUtc);
        command.Parameters.AddWithValue("next", nextUtc);
        command.Parameters.AddWithValue("now", nowUtc);
        if (!initialize) command.Parameters.AddWithValue("due", observed.NextDigestAt ?? throw new ArgumentException("due_slot_required"));
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false) == 1;
    }

    private sealed class TickLease(NpgsqlConnection connection) : IScheduledTickLease
    {
        /// <summary>Checks that the open session still owns the scheduler advisory lock.</summary>
        /// <exception cref="GenerationStoppedException">The connection is not open or the scheduler lock is absent.</exception>
        /// <remarks>Database errors and cancellation propagate.</remarks>
        public async Task EnsureHeldAsync(CancellationToken ct)
        {
            if (connection.State != ConnectionState.Open) throw new GenerationStoppedException("schedule_lease_lost");
            await using var command = new NpgsqlCommand("""
                SELECT EXISTS(SELECT 1 FROM pg_locks WHERE locktype = 'advisory' AND pid = pg_backend_pid()
                AND classid = 0 AND objid = 1280200760 AND objsubid = 1 AND granted)
                """, connection);
            if (!(bool)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false))!)
                throw new GenerationStoppedException("schedule_lease_lost");
        }

        /// <summary>Closes the dedicated connection, releasing its scheduler session lock.</summary>
        public async ValueTask DisposeAsync()
        {
            // This non-pooled connection owns only the scheduler lease; closing it releases the session lock,
            // including when cancellation or a broken connection makes an explicit unlock impossible.
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
