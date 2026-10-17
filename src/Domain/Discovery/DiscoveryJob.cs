using System.Text;
using LinerNotes.Domain.Common;

namespace LinerNotes.Domain.Discovery;

public enum DiscoveryJobKind { Generation, Lookup }
public enum DiscoveryJobState { Pending, Running, Retryable, Completed, Failed, Cancelled }

/// <summary>Durable request data. Lease execution is introduced before job submission is enabled.</summary>
public sealed class DiscoveryJob : BaseEntity
{
    public Guid ProfileId { get; private set; }
    public string RequestKey { get; private set; } = "";
    public string RequestHash { get; private set; } = "";
    public DiscoveryJobKind Kind { get; private set; }
    public DiscoveryJobState State { get; private set; }
    public long InputRevision { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public int Attempts { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public string ProgressCode { get; private set; } = "queued";
    public string? ErrorCode { get; private set; }
    public Guid? ResultBatchId { get; private set; }
    public string? ResultJson { get; private set; }

    private DiscoveryJob() { }

    public DiscoveryJob(Guid profileId, string requestKey, string requestHash, DiscoveryJobKind kind,
        long inputRevision, string payloadJson, DateTimeOffset now)
    {
        ProfileId = DiscoveryRules.Identity(profileId);
        RequestKey = DiscoveryRules.RequestKey(requestKey);
        if (requestHash is not { Length: 64 } || requestHash.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ArgumentException("Invalid request hash.");
        if (!Enum.IsDefined(kind)) throw new ArgumentException("Invalid job kind.");
        if (string.IsNullOrWhiteSpace(payloadJson) || Encoding.UTF8.GetByteCount(payloadJson) > 16_384)
            throw new ArgumentException("Job payload exceeds its bound.");
        RequestHash = requestHash;
        Kind = kind;
        State = DiscoveryJobState.Pending;
        InputRevision = DiscoveryRules.Revision(inputRevision);
        PayloadJson = payloadJson;
        CreatedAtUtc = UpdatedAtUtc = NextAttemptAtUtc = DiscoveryRules.Utc(now);
    }

    public bool IsUnfinished => State is DiscoveryJobState.Pending or DiscoveryJobState.Running or DiscoveryJobState.Retryable;
    public void AcquireLease(Guid token, DateTimeOffset now, TimeSpan duration, int maximumAttempts)
    {
        now = DiscoveryRules.Utc(now);
        ValidateDuration(duration);
        if (!IsUnfinished || (State == DiscoveryJobState.Running ? LeaseExpiresAtUtc > now : NextAttemptAtUtc > now))
            throw new InvalidOperationException("job_not_available");
        if (maximumAttempts is < 1 or > 10 || Attempts >= maximumAttempts) throw new InvalidOperationException("job_attempts_exhausted");
        LeaseToken = DiscoveryRules.Identity(token);
        LeaseExpiresAtUtc = now.Add(duration);
        State = DiscoveryJobState.Running;
        Attempts = checked(Attempts + 1);
        UpdatedAtUtc = now;
        ProgressCode = "preparing";
        ErrorCode = null;
    }
    public void RequireLease(Guid token, DateTimeOffset now)
    {
        if (State != DiscoveryJobState.Running || LeaseToken != token || LeaseExpiresAtUtc is not { } expiry || expiry <= DiscoveryRules.Utc(now))
            throw new InvalidOperationException("job_lease_lost");
    }
    public void RenewLease(Guid token, DateTimeOffset now, TimeSpan duration, string progress)
    {
        RequireLease(token, now); ValidateDuration(duration);
        ProgressCode = Code(progress);
        LeaseExpiresAtUtc = now.Add(duration); UpdatedAtUtc = now;
    }
    public void Complete(Guid token, DateTimeOffset now, Guid? batchId, string? resultJson)
    {
        RequireLease(token, now);
        if ((Kind == DiscoveryJobKind.Generation && (!batchId.HasValue || batchId == Guid.Empty)) ||
            (Kind == DiscoveryJobKind.Lookup && (batchId.HasValue || string.IsNullOrWhiteSpace(resultJson))) ||
            (resultJson is not null && Encoding.UTF8.GetByteCount(resultJson) > 32_768))
            throw new ArgumentException("job_result_invalid");
        ResultBatchId = batchId; ResultJson = resultJson;
        State = DiscoveryJobState.Completed; ProgressCode = "completed";
        ClearLease(now, clearPayload: true);
    }
    public void Fail(Guid token, DateTimeOffset now, string code, bool retryable, TimeSpan delay, int maximumAttempts)
    {
        RequireLease(token, now);
        if (delay < TimeSpan.Zero || delay > TimeSpan.FromMinutes(10) || maximumAttempts is < 1 or > 10)
            throw new ArgumentException("job_retry_invalid");
        ErrorCode = Code(code);
        State = retryable && Attempts < maximumAttempts ? DiscoveryJobState.Retryable : DiscoveryJobState.Failed;
        ProgressCode = State == DiscoveryJobState.Retryable ? "waiting_to_retry" : "failed";
        NextAttemptAtUtc = now.Add(delay);
        ClearLease(now, clearPayload: State == DiscoveryJobState.Failed);
    }
    public void Cancel(DateTimeOffset now, string code)
    {
        if (!IsUnfinished) return;
        State = DiscoveryJobState.Cancelled; ProgressCode = "cancelled"; ErrorCode = Code(code);
        ClearLease(now, clearPayload: true);
    }
    private void ClearLease(DateTimeOffset now, bool clearPayload)
    {
        LeaseToken = null; LeaseExpiresAtUtc = null; UpdatedAtUtc = DiscoveryRules.Utc(now);
        if (clearPayload) PayloadJson = "{}";
    }
    private static void ValidateDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.FromSeconds(5) || duration > TimeSpan.FromMinutes(2)) throw new ArgumentException("job_lease_duration_invalid");
    }
    private static string Code(string value) => !string.IsNullOrEmpty(value) && value.Length <= 64 &&
        value.All(c => c is >= 'a' and <= 'z' or '_') ? value : throw new ArgumentException("job_code_invalid");
}
