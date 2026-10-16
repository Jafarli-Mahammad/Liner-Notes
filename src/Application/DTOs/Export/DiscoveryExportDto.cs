using System.Text.Json;

namespace LinerNotes.Application.DTOs.Export;

public sealed record ListenerProfileExportDto(Guid Id, Guid? UserId, long Revision,
    DateTimeOffset CreatedAtUtc, DateTimeOffset LastActiveAtUtc, DateTimeOffset? ExpiresAtUtc);

public sealed record DiscoveryBatchExportDto(Guid Id, Guid ProfileId, string RequestId, string Kind,
    long InputRevision, DateTimeOffset CreatedAtUtc, DateTimeOffset? CompletedAtUtc,
    string? CatalogVersion, string? EvidenceVersion);

/// <summary>Operational lease tokens are security internals and are deliberately excluded.</summary>
public sealed record DiscoveryJobExportDto(Guid Id, Guid ProfileId, string RequestKey, string RequestHash,
    string Kind, string State, long InputRevision, JsonElement PayloadJson, int Attempts,
    DateTimeOffset? LeaseExpiresAtUtc, DateTimeOffset NextAttemptAtUtc, DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc, string ProgressCode, string? ErrorCode, Guid? ResultBatchId, JsonElement? ResultJson);

public sealed record DiscoveryExportDto(ListenerProfileExportDto? Profile,
    IReadOnlyList<DiscoveryBatchExportDto> Batches, IReadOnlyList<DiscoveryJobExportDto> Jobs,
    IReadOnlyList<LinerNotes.Application.DTOs.Digests.WeeklyRecommendationDto>? Recommendations = null);
