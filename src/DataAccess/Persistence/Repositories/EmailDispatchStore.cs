using System.Net;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Application.Services;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Email;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class EmailDispatchStore(DataContext db, IUnitOfWork unit, IDiscoveryWriteCoordinator admission,
    DiscoveryRuntimeOptions runtime, IEmailPayloadProtection protection, IAccountEmailLease leases) : IEmailDispatchStore
{
    private DbSet<EmailDispatch> Rows => db.Set<EmailDispatch>();
    private Task<T> WriteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct) =>
        runtime.ManagedWritesEnabled && db.Database.CurrentTransaction is null
            ? admission.ExecuteAsync(524_288, token => unit.ExecuteInTransactionAsync(action, token), ct)
            : unit.ExecuteInTransactionAsync(action, ct);
    public async Task<bool> SetPreferenceAsync(Guid userId, bool optIn, CancellationToken ct)
    {
        await using var lease = await leases.AcquireAsync(userId, ct);
        return await WriteAsync(async token => await db.Users.Where(x => x.Id == userId && !x.IsDeleted)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.WeeklyEmailOptIn, optIn)
                .SetProperty(x => x.EmailUnsubscribedAtUtc, optIn ? null : DateTime.UtcNow)
                .SetProperty(x => x.LastModifiedAt, DateTime.UtcNow), token) == 1, ct);
    }
    public Task<EmailAccount?> AccountAsync(Guid id, CancellationToken ct) =>
        (from user in db.Users
         where user.Id == id && !user.IsDeleted
         join account in db.ApplicationUsers on user.Id equals account.Id
         where !account.IsDeleted && account.Email != null
         select new EmailAccount(user.Id, account.Email!, account.EmailConfirmed, user.WeeklyEmailOptIn,
             user.EmailUnsubscribedAtUtc != null, db.Set<EmailSuppression>().Any(s => s.UserId == user.Id), account.SecurityStamp)).SingleOrDefaultAsync(ct);
    public Task<bool> EnqueueAsync(EmailDispatch dispatch, TimeSpan cooldown, CancellationToken ct) => WriteAsync(async token =>
    {
        if (await Rows.AnyAsync(x => x.RequestKey == dispatch.RequestKey, token)) return true;
        if (cooldown > TimeSpan.Zero && await Rows.AnyAsync(x => x.UserId == dispatch.UserId && x.Purpose == dispatch.Purpose &&
            x.CreatedAt > dispatch.CreatedAt.Subtract(cooldown), token)) return false;
        if (await AccountAsync(dispatch.UserId, token) is null) return false;
        Rows.Add(dispatch); await db.SaveChangesAsync(token); return true;
    }, ct);
    public async Task<IReadOnlyList<EmailDispatch>> DueAsync(int limit, DateTimeOffset now, CancellationToken ct)
    {
        var query = Rows.Where(x => (x.State == EmailState.Queued || x.State == EmailState.Retryable || x.State == EmailState.Attempting) && x.NextAttemptAt <= now);
        int weeklyLimit = Math.Max(1, limit / 5);
        var weekly = await query.Where(x => x.Purpose == EmailPurpose.WeeklyDigest).OrderBy(x => x.NextAttemptAt).ThenBy(x => x.Id).Take(weeklyLimit).ToArrayAsync(ct);
        var security = await query.Where(x => x.Purpose != EmailPurpose.WeeklyDigest).OrderBy(x => x.NextAttemptAt).ThenBy(x => x.Id).Take(limit - weekly.Length).ToArrayAsync(ct);
        var result = security.Concat(weekly).ToList();
        if (result.Count < limit)
        {
            var ids = result.Select(x => x.Id).ToArray();
            result.AddRange(await query.Where(x => !ids.Contains(x.Id)).OrderBy(x => x.NextAttemptAt).ThenBy(x => x.Id).Take(limit - result.Count).ToArrayAsync(ct));
        }
        return result;
    }
    public Task<EmailDispatch?> ChangeAsync(Guid id, Action<EmailDispatch> change, CancellationToken ct) => WriteAsync<EmailDispatch?>(async token =>
    {
        var row = Rows.Local.FirstOrDefault(x => x.Id == id) ?? await Rows.AsTracking().SingleOrDefaultAsync(x => x.Id == id, token);
        if (row is null) return null;
        change(row);
        if (row.State == EmailState.Accepted && row.DigestId is { } digestId)
        {
            var digest = await db.WeeklyDigests.AsTracking().SingleOrDefaultAsync(x => x.Id == digestId && x.UserId == row.UserId, token);
            if (digest is null) throw new InvalidOperationException("email_digest_missing");
            digest.MarkSent(row.AcceptedAt!.Value.UtcDateTime);
        }
        await db.SaveChangesAsync(token); return row;
    }, ct);
    public async Task<int> AttemptedTodayAsync(DateTimeOffset now, CancellationToken ct)
    {
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        return await Rows.Where(x => x.LastAttemptAt >= day).SumAsync(x => (int?)x.Attempts, ct) ?? 0;
    }
    public async Task<bool> EventAsync(EmailDeliveryEvent value, CancellationToken ct)
    {
        var identity = await Rows.Where(x => x.ProviderMessageId == value.MessageId).Select(x => new { x.Id, x.UserId }).SingleOrDefaultAsync(ct);
        if (identity is null) return false;
        await using var ownership = await leases.AcquireAsync(identity.UserId, ct);
        return await WriteAsync(async token =>
        {
            var row = await Rows.AsTracking().SingleOrDefaultAsync(x => x.Id == identity.Id, token);
            if (row is null) return true;
            row.ApplyEvent(value.Outcome, value.OccurredAt);
            if (value.Outcome is "bounced" or "complained" or "suppressed")
            {
                var account = await AccountAsync(row.UserId, token);
                if (account is not null && EmailQueue.RecipientHash(account.Email) == row.RecipientHash && !account.Suppressed)
                    db.Set<EmailSuppression>().Add(new(row.UserId, row.RecipientHash, value.Outcome, value.OccurredAt));
            }
            await db.SaveChangesAsync(token); return true;
        }, ct);
    }
    public async Task<IReadOnlyList<EmailSuppressionExport>> SuppressionsAsync(Guid userId, CancellationToken ct) =>
        await db.Set<EmailSuppression>().Where(x => x.UserId == userId)
            .Select(x => new EmailSuppressionExport(x.UserId, x.RecipientHash, x.Reason, x.CreatedAt)).ToArrayAsync(ct);
    public async Task<OperationalMail> SummaryAsync(DateTimeOffset now, int dailyLimit, CancellationToken ct)
    {
        var pending = Rows.Where(x => x.State == EmailState.Queued || x.State == EmailState.Retryable || x.State == EmailState.Attempting);
        return new(await pending.CountAsync(ct), await Rows.CountAsync(x => x.State == EmailState.Ambiguous, ct),
            await Rows.CountAsync(x => x.State == EmailState.Failed && x.OutcomeAt > now.AddHours(-24), ct),
            await db.Set<EmailSuppression>().CountAsync(ct), Math.Max(0, dailyLimit - await AttemptedTodayAsync(now, ct)),
            await pending.MinAsync(x => (DateTimeOffset?)x.CreatedAt, ct));
    }
    public async Task<int> PruneAsync(DateTimeOffset now, CancellationToken ct)
    {
        var expired = await Rows.Where(x => x.ProtectedPayload != null && x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt).Select(x => new { x.Id, x.UserId }).Take(100).ToArrayAsync(ct);
        foreach (var item in expired)
        {
            // Never clear an in-flight payload while its sender owns the account lease.
            await using var ownership = await leases.AcquireAsync(item.UserId, ct);
            await ChangeAsync(item.Id, row =>
            {
                if (row.Pending && row.ExpiresAt <= now)
                    row.Finish(row.FirstAttemptAt is null ? EmailState.Cancelled : EmailState.Ambiguous, now, "payload_expired");
            }, ct);
        }
        return await WriteAsync(async token =>
        {
            var ids = await Rows.Where(x => x.OutcomeAt < now.AddDays(-90)).OrderBy(x => x.OutcomeAt).Select(x => x.Id).Take(100).ToArrayAsync(token);
            return await Rows.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(token);
        }, ct);
    }
    public async Task<IReadOnlyList<EmailDispatchExport>> ExportAsync(Guid userId, CancellationToken ct)
    {
        var rows = await Rows.Where(x => x.UserId == userId).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
        return rows.Select(row => new EmailDispatchExport(row.Id, row.UserId, row.DigestId, row.Purpose.ToString(), row.RequestKey,
            row.RecipientHash, row.Fingerprint, row.State.ToString(), row.CreatedAt, row.ExpiresAt, row.NextAttemptAt,
            row.FirstAttemptAt, row.LastAttemptAt, row.Attempts, row.ProviderMessageId, row.AcceptedAt, row.DeliveredAt,
            row.OutcomeAt, row.Reason, SafePayload(row.ProtectedPayload))).ToArray();
    }
    private EmailPayload? SafePayload(string? protectedPayload)
    {
        if (protectedPayload is null) return null;
        var payload = JsonSerializer.Deserialize<EmailPayload>(protection.Unprotect(protectedPayload)) ?? throw new InvalidOperationException("email_payload_invalid");
        string Redact(string value)
        {
            foreach (var link in payload.SensitiveLinks) value = value.Replace(WebUtility.HtmlEncode(link), "[capability redacted]", StringComparison.Ordinal).Replace(link, "[capability redacted]", StringComparison.Ordinal);
            return value;
        }
        return payload with { Text = Redact(payload.Text), Html = Redact(payload.Html), Headers = new Dictionary<string, string>(), SensitiveLinks = [], SecurityStamp = null };
    }
    public async Task<IAsyncDisposable?> TryAcquireDispatchLeaseAsync(CancellationToken ct)
    {
        var settings = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString()) { Pooling = false };
        var connection = new NpgsqlConnection(settings.ConnectionString);
        try
        {
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(1280200761)", connection);
            if ((bool)(await command.ExecuteScalarAsync(ct))!) return new LinerNotes.DataAccess.Persistence.MonitoredEmailLease(connection);
            await connection.DisposeAsync(); return null;
        }
        catch { await connection.DisposeAsync(); throw; }
    }
}
