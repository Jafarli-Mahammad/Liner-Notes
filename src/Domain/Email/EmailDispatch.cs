namespace LinerNotes.Domain.Email;

public enum EmailPurpose { Confirmation, PasswordReset, WeeklyDigest }
public enum EmailState { Queued, Attempting, Retryable, Accepted, Delivered, Failed, Cancelled, Ambiguous, Bounced, Complained }

public sealed class EmailDispatch
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? DigestId { get; private set; }
    public EmailPurpose Purpose { get; private set; }
    public string RequestKey { get; private set; } = "";
    public string RecipientHash { get; private set; } = "";
    public string Fingerprint { get; private set; } = "";
    public string? ProtectedPayload { get; private set; }
    public EmailState State { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? FirstAttemptAt { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public int Attempts { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? OutcomeAt { get; private set; }
    public string? Reason { get; private set; }
    public bool Pending => State is EmailState.Queued or EmailState.Attempting or EmailState.Retryable;
    private EmailDispatch() { }
    public EmailDispatch(Guid userId, Guid? digestId, EmailPurpose purpose, string key, string recipientHash,
        string fingerprint, string payload, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        if (userId == Guid.Empty || !Enum.IsDefined(purpose) || string.IsNullOrWhiteSpace(key) || key.Length > 200 ||
            recipientHash.Length != 64 || fingerprint.Length != 64 || payload.Length is < 1 or > 350_000 ||
            expiresAt <= now || expiresAt > now.AddDays(7) || (purpose == EmailPurpose.WeeklyDigest) != digestId.HasValue)
            throw new ArgumentException("invalid_email_dispatch");
        Id = Guid.NewGuid(); UserId = userId; DigestId = digestId; Purpose = purpose; RequestKey = key;
        RecipientHash = recipientHash; Fingerprint = fingerprint; ProtectedPayload = payload;
        CreatedAt = NextAttemptAt = now; ExpiresAt = expiresAt;
    }
    public bool BeginAttempt(DateTimeOffset now)
    {
        if (!Pending) return false;
        if (ExpiresAt <= now || FirstAttemptAt is { } first && first.AddHours(23) <= now || Attempts >= 20)
        {
            Finish(FirstAttemptAt is null ? EmailState.Cancelled : EmailState.Ambiguous, now, "retry_window_closed");
            return false;
        }
        if (NextAttemptAt > now) return false;
        FirstAttemptAt ??= now; LastAttemptAt = now; Attempts++; State = EmailState.Attempting;
        return true;
    }
    public void Retry(DateTimeOffset now, DateTimeOffset retryAt, string reason)
    {
        if (State != EmailState.Attempting) throw new InvalidOperationException("email_not_attempting");
        State = EmailState.Retryable; NextAttemptAt = retryAt > now ? retryAt : now.AddSeconds(30); Reason = reason;
    }
    public void Accept(string messageId, DateTimeOffset now)
    {
        if (State != EmailState.Attempting || string.IsNullOrWhiteSpace(messageId) || messageId.Length > 200)
            throw new InvalidOperationException("invalid_email_acceptance");
        ProviderMessageId = messageId; AcceptedAt = now;
        Finish(EmailState.Accepted, now, null);
    }
    public void Finish(EmailState state, DateTimeOffset now, string? reason)
    {
        if (state is not (EmailState.Cancelled or EmailState.Ambiguous or EmailState.Failed or EmailState.Accepted))
            throw new ArgumentException("invalid_email_terminal_state");
        State = state; OutcomeAt = now; Reason = reason; ProtectedPayload = null;
    }
    public void ApplyEvent(string outcome, DateTimeOffset now)
    {
        if (ProviderMessageId is null) throw new InvalidOperationException("email_not_accepted");
        if (outcome == "delivered" && State is EmailState.Accepted or EmailState.Failed)
        { State = EmailState.Delivered; DeliveredAt = now; OutcomeAt = now; Reason = null; }
        else if (outcome is "bounced" or "complained")
        {
            if (State == EmailState.Complained) return;
            State = outcome == "bounced" ? EmailState.Bounced : EmailState.Complained; OutcomeAt = now;
        }
        else if (outcome is "failed" or "suppressed" && State == EmailState.Accepted)
        { State = EmailState.Failed; OutcomeAt = now; Reason = "provider_" + outcome; }
    }
}
