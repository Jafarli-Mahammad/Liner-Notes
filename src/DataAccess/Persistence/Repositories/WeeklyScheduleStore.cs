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

    private static ScheduledUser Read(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1),
        reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetDateTime(3));

    public Task<bool> InitializeAsync(ScheduledUser observed, DateTime nextUtc, DateTime nowUtc, CancellationToken ct) =>
        UpdateAsync(observed, nextUtc, nowUtc, initialize: true, ct);

    public Task<bool> AdvanceAsync(ScheduledUser observed, DateTime nextUtc, DateTime nowUtc, CancellationToken ct) =>
        UpdateAsync(observed, nextUtc, nowUtc, initialize: false, ct);

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

        public async ValueTask DisposeAsync()
        {
            // This non-pooled connection owns only the scheduler lease; closing it releases the session lock,
            // including when cancellation or a broken connection makes an explicit unlock impossible.
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
