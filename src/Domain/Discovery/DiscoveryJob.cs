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
}
