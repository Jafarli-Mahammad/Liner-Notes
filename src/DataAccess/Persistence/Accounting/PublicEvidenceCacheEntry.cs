using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Models.Recommendation;
namespace LinerNotes.DataAccess.Persistence.Accounting;
/// <summary>Public music facts and acquisition coordination only. Never a listener or query log.</summary>
public sealed class PublicEvidenceCacheEntry
{
    public string Key { get; set; } = "";
    public PublicEvidenceKind Kind { get; set; }
    public string? Provider { get; set; }
    public EvidenceOrigin? Origin { get; set; }
    public string? Version { get; set; }
    public string? PayloadJson { get; set; }
    public string? ContentSha256 { get; set; }
    public long StoredBytes { get; private set; }
    public DateTimeOffset? AcquiredAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
