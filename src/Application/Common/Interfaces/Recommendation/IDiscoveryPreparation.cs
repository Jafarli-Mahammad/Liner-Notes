using LinerNotes.Application.Common.Models.Recommendation;
namespace LinerNotes.Application.Common.Interfaces.Recommendation;

public interface ICanonicalRecommendationSource
{
    Task<IngestionResult<RawCandidateTrack>> GetCandidatesByCanonicalArtistsAsync(IReadOnlyList<Guid> artistIds, int limit, CancellationToken ct);
}
public interface ICanonicalSeedTagSource
{
    Task<SeedTagSnapshot> GetArtistAsync(Guid artistId, string name, CancellationToken ct);
    Task<SeedTagSnapshot> GetAlbumAsync(Guid releaseGroupId, string name, CancellationToken ct);
}
public interface IPublicEvidenceFreshness { DateTimeOffset? SharedUntilUtc { get; } }
public sealed record DiscoveryPoolRequest(Guid? ArtistId, string Seed, bool IsTag, bool IsRecording = false);
public sealed record DiscoverySeedRequest(Guid? EntityId, string Name, bool IsAlbum, bool IsRecording = false);
public interface IDiscoverySeedPreparation
{
    Task<SeedTagSnapshot> GetAsync(DiscoverySeedRequest request, bool allowAcquisition, CancellationToken ct);
}
public sealed record PreparedDiscoveryPool(string Key, string Provider, EvidenceOrigin Origin, DateTimeOffset PreparedAtUtc,
    DateTimeOffset? ExpiresAtUtc, string NormalizationVersion, IReadOnlyList<HydratedCandidateTrack> Candidates,
    IReadOnlyList<CoverageGap> Gaps, long SerializedBytes, string ContentSha256);
/// <summary>Only public per-entity evidence is reusable. Taste, exclusions and ranking remain per profile.</summary>
public interface IDiscoveryPreparation
{
    Task<PreparedDiscoveryPool> GetAsync(DiscoveryPoolRequest request, CancellationToken ct);
    Task<PreparedDiscoveryPool> GetAsync(DiscoveryPoolRequest request, bool allowAcquisition, CancellationToken ct);
}
