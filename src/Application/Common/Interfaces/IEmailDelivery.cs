using LinerNotes.Domain.Email;

namespace LinerNotes.Application.Common.Interfaces;

public sealed class EmailDeliveryOptions
{
    public bool Enabled { get; set; }
    public string PublicOrigin { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public int DailyLimit { get; set; } = 90;
    public void Validate()
    {
        if (!Uri.TryCreate(PublicOrigin, UriKind.Absolute, out var origin) || origin.UserInfo.Length != 0 ||
            PublicOrigin != origin.GetLeftPart(UriPartial.Authority) || origin.Scheme is not ("https" or "http") ||
            origin.Scheme == "http" && !origin.IsLoopback ||
            !System.Net.Mail.MailAddress.TryCreate(FromAddress, out var address) || address.Address != FromAddress ||
            FromAddress.Any(char.IsControl) || DailyLimit is < 1 or > 100_000)
            throw new InvalidOperationException("email_configuration_invalid");
    }
}
public sealed record EmailPayload(string To, string Subject, string Text, string Html,
    IReadOnlyDictionary<string, string> Headers, IReadOnlyList<string> SensitiveLinks, string? From = null, string? SecurityStamp = null);
public sealed record EmailAccount(Guid Id, string Email, bool Confirmed, bool OptedIn, bool Unsubscribed, bool Suppressed, string? SecurityStamp = null);
public sealed record EmailTransportResult(string Status, string? MessageId = null, TimeSpan? RetryAfter = null);
public sealed record OperationalMail(int Pending, int Ambiguous, int RecentFailures, int Suppressed, int RemainingDailyBudget, DateTimeOffset? OldestPendingAt);
public sealed record EmailSuppressionExport(Guid UserId, string RecipientHash, string Reason, DateTimeOffset CreatedAt);
public sealed record EmailDeliveryEvent(string MessageId, string Outcome, DateTimeOffset OccurredAt);
public sealed record EmailDispatchExport(Guid Id, Guid UserId, Guid? DigestId, string Purpose, string RequestKey,
    string RecipientHash, string Fingerprint, string State, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt,
DateTimeOffset NextAttemptAt, DateTimeOffset? FirstAttemptAt, DateTimeOffset? LastAttemptAt, int Attempts, int AttemptsToday,
    string? ProviderMessageId, DateTimeOffset? AcceptedAt, DateTimeOffset? DeliveredAt, DateTimeOffset? OutcomeAt,
    string? Reason, EmailPayload? RetainedMessage);
public interface IEmailLeaseGuard : IAsyncDisposable { CancellationToken Lost { get; } }
public interface IEmailPayloadProtection { void Prepare(); string Protect(string value); string Unprotect(string value); }
public interface IEmailTransport
{
    Task<EmailTransportResult> SendAsync(EmailPayload payload, string idempotencyKey, CancellationToken ct);
    Task<EmailTransportResult> ReconcileAsync(string messageId, CancellationToken ct);
}
public interface IEmailEventVerifier
{
    EmailDeliveryEvent? Verify(byte[] body, string? id, string? timestamp, string? signature);
}
public interface IEmailDispatchStore
{
    Task<bool> SetPreferenceAsync(Guid userId, bool optIn, CancellationToken ct);
    Task<EmailAccount?> AccountAsync(Guid id, CancellationToken ct);
    Task<bool> EnqueueAsync(EmailDispatch dispatch, TimeSpan cooldown, CancellationToken ct);
    Task<IReadOnlyList<EmailDispatch>> DueAsync(int limit, DateTimeOffset now, CancellationToken ct);
    Task<EmailDispatch?> ChangeAsync(Guid id, Action<EmailDispatch> change, CancellationToken ct);
    Task<int> AttemptedTodayAsync(DateTimeOffset now, CancellationToken ct);
    Task<bool> EventAsync(EmailDeliveryEvent value, CancellationToken ct);
    Task<IReadOnlyList<EmailSuppressionExport>> SuppressionsAsync(Guid userId, CancellationToken ct);
    Task<OperationalMail> SummaryAsync(DateTimeOffset now, int dailyLimit, CancellationToken ct);
    Task<int> PruneAsync(DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<EmailDispatchExport>> ExportAsync(Guid userId, CancellationToken ct);
    Task<IAsyncDisposable?> TryAcquireDispatchLeaseAsync(CancellationToken ct);
}
