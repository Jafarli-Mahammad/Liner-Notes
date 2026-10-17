using LinerNotes.Application.Common.Models.Recommendation;
namespace LinerNotes.Application.Common.Interfaces.Discovery;
public enum PublicEvidenceKind { Pool, Seed, Lookup }
public sealed record PublicEvidenceDocument(string Key, PublicEvidenceKind Kind, string Provider, EvidenceOrigin Origin,
    string Version, string PayloadJson, string ContentSha256, DateTimeOffset AcquiredAtUtc, DateTimeOffset ExpiresAtUtc);
public sealed record PublicEvidenceLease(string Key, Guid Token, DateTimeOffset ExpiresAtUtc);
public interface IPublicEvidenceStore
{
    Task<PublicEvidenceDocument?> FindAsync(string key, DateTimeOffset now, CancellationToken ct);
    Task<PublicEvidenceLease?> ClaimAsync(string key, PublicEvidenceKind kind, DateTimeOffset now, TimeSpan duration, CancellationToken ct);
    Task PublishAsync(PublicEvidenceLease lease, PublicEvidenceDocument document, DateTimeOffset now, long maximumBytes, int maximumEntries, CancellationToken ct);
    Task ReleaseAsync(PublicEvidenceLease lease, DateTimeOffset now, CancellationToken ct);
    Task<int> DeleteExpiredAsync(DateTimeOffset now, int limit, CancellationToken ct);
}
public sealed class DiscoveryPreparationRequiredException : Exception { }
public sealed class PublicPreparationBusyException : Exception { }
