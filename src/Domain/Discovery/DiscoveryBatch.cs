using LinerNotes.Domain.Common;

namespace LinerNotes.Domain.Discovery;

public enum DiscoveryBatchKind { Initial, OnDemand, Weekly }

/// <summary>One explicit recommendation request; independent of calendar delivery identity.</summary>
public sealed class DiscoveryBatch : BaseEntity
{
    public Guid ProfileId { get; private set; }
    public string RequestId { get; private set; } = "";
    public DiscoveryBatchKind Kind { get; private set; }
    public long InputRevision { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public string? CatalogVersion { get; private set; }
    public string? EvidenceVersion { get; private set; }

    private DiscoveryBatch() { }

    public DiscoveryBatch(Guid profileId, string requestId, DiscoveryBatchKind kind, long inputRevision,
        DateTimeOffset now, Guid? id = null)
    {
        ProfileId = DiscoveryRules.Identity(profileId);
        RequestId = DiscoveryRules.RequestKey(requestId);
        if (!Enum.IsDefined(kind)) throw new ArgumentException("Invalid batch kind.");
        Kind = kind;
        InputRevision = DiscoveryRules.Revision(inputRevision);
        CreatedAtUtc = DiscoveryRules.Utc(now);
        if (id is { } identity) Id = DiscoveryRules.Identity(identity);
    }

    public void Complete(DateTimeOffset now)
    {
        now = DiscoveryRules.Utc(now);
        if (now < CreatedAtUtc) throw new ArgumentException("Completion precedes creation.");
        CompletedAtUtc ??= now;
    }
}
