using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Domain.Email;

namespace LinerNotes.Application.Services;

public sealed class EmailQueue(IEmailDispatchStore store, IEmailPayloadProtection protection,
    EmailDeliveryOptions options, TimeProvider clock)
{
    public bool Enabled => options.Enabled;
    public static string RecipientHash(string email) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToUpperInvariant())));
    public async Task<bool> EnqueueAsync(Guid userId, Guid? digestId, EmailPurpose purpose, string key,
        EmailPayload payload, TimeSpan lifetime, TimeSpan cooldown, CancellationToken ct)
    {
        if (!Enabled) return false;
        options.Validate();
        var account = await store.AccountAsync(userId, ct);
        if (account is null || !string.Equals(account.Email, payload.To, StringComparison.OrdinalIgnoreCase) || account.Suppressed ||
            purpose == EmailPurpose.Confirmation && account.Confirmed ||
            purpose == EmailPurpose.WeeklyDigest && (!account.Confirmed || !account.OptedIn || account.Unsubscribed)) return false;
        payload = payload with { From = options.FromAddress };
        var json = JsonSerializer.Serialize(payload);
        if (Encoding.UTF8.GetByteCount(json) > 240_000) throw new ArgumentException("email_payload_too_large");
        var now = clock.GetUtcNow();
        return await store.EnqueueAsync(new EmailDispatch(userId, digestId, purpose, key, RecipientHash(payload.To),
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))), protection.Protect(json), now, now.Add(lifetime)), cooldown, ct);
    }
}
public sealed record EmailTickResult(int Accepted, int Deferred, int Stopped, bool Busy = false);
public sealed class EmailDispatcher(IEmailDispatchStore store, IEmailPayloadProtection protection, IEmailTransport transport,
    IAccountEmailLease ownership, EmailDeliveryOptions options, TimeProvider clock)
{
    public async Task<EmailTickResult> RunAsync(int limit, CancellationToken ct)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        if (!options.Enabled) return new(0, 0, 0);
        options.Validate();
        await using var tick = await store.TryAcquireDispatchLeaseAsync(ct);
        if (tick is null) return new(0, 0, 0, true);
        await store.PruneAsync(clock.GetUtcNow(), ct);
        int accepted = 0, deferred = 0, stopped = 0;
        foreach (var item in await store.DueAsync(limit, clock.GetUtcNow(), ct))
        {
            (tick as IEmailLeaseGuard)?.Lost.ThrowIfCancellationRequested();
            ct.ThrowIfCancellationRequested();
            await using var accountLease = await ownership.AcquireAsync(item.UserId, ct);
            using var sendDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct,
                (tick as IEmailLeaseGuard)?.Lost ?? CancellationToken.None, (accountLease as IEmailLeaseGuard)?.Lost ?? CancellationToken.None);
            var account = await store.AccountAsync(item.UserId, ct);
            var now = clock.GetUtcNow();
            if (account is null || account.Suppressed || EmailQueue.RecipientHash(account.Email) != item.RecipientHash ||
                item.Purpose == EmailPurpose.Confirmation && account.Confirmed ||
                item.Purpose == EmailPurpose.WeeklyDigest && (!account.Confirmed || !account.OptedIn || account.Unsubscribed))
            { await store.ChangeAsync(item.Id, row => row.Finish(EmailState.Cancelled, now, "recipient_ineligible"), ct); stopped++; continue; }
            if (await store.AttemptedTodayAsync(now, ct) >= options.DailyLimit) { deferred++; continue; }
            var current = await store.ChangeAsync(item.Id, row => row.BeginAttempt(now), ct);
            if (current?.State != EmailState.Attempting || current.ProtectedPayload is null) { stopped++; continue; }
            EmailTransportResult result;
            try
            {
                var payload = JsonSerializer.Deserialize<EmailPayload>(protection.Unprotect(current.ProtectedPayload))
                    ?? throw new InvalidOperationException("email_payload_invalid");
                if (item.Purpose != EmailPurpose.WeeklyDigest && payload.SecurityStamp != account.SecurityStamp)
                {
                    await store.ChangeAsync(item.Id, row => row.Finish(EmailState.Cancelled, now, "security_state_changed"), ct);
                    stopped++; continue;
                }
                result = await transport.SendAsync(payload, current.Id.ToString("N"), sendDeadline.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
            { result = new("unknown"); }
            catch (Exception error) when (error is System.Security.Cryptography.CryptographicException or JsonException or ArgumentException or InvalidOperationException)
            { result = new("rejected"); }
            sendDeadline.Token.ThrowIfCancellationRequested();
            now = clock.GetUtcNow();
            await store.ChangeAsync(item.Id, row =>
            {
                if (result.Status == "accepted" && result.MessageId is not null) row.Accept(result.MessageId, now);
                else if (result.Status == "rejected") row.Finish(EmailState.Failed, now, "transport_rejected");
                else row.Retry(now, now.Add(result.RetryAfter ?? TimeSpan.FromSeconds(Math.Min(3600, 30 * Math.Pow(2, row.Attempts)))), "transport_" + (result.Status == "retry" ? "retry" : "unknown"));
            }, ct);
            if (result.Status == "accepted") accepted++; else if (result.Status == "rejected") stopped++; else deferred++;
        }
        return new(accepted, deferred, stopped);
    }
    public async Task<string> ReconcileAsync(string messageId, CancellationToken ct)
    {
        if (!options.Enabled || !Guid.TryParse(messageId, out _)) throw new ArgumentException("valid_message_id_required");
        var result = await transport.ReconcileAsync(messageId, ct);
        if (result.Status is not ("delivered" or "bounced" or "complained" or "failed" or "suppressed")) return "unresolved";
        return await store.EventAsync(new(messageId, result.Status, clock.GetUtcNow()), ct) ? "reconciled" : "not_found";
    }
}
