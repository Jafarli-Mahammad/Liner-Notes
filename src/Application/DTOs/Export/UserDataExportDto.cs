using LinerNotes.Application.DTOs.Digests;
using LinerNotes.Application.DTOs.Subscribers;
using LinerNotes.Application.DTOs.Taste;

namespace LinerNotes.Application.DTOs.Export;

/// <summary>
/// Connected external music service record for export.
/// </summary>
public record UserMusicConnectionExportDto(
    string ServiceType,
    string ExternalUsername,
    DateTime? LastSyncedAt,
    bool IsActive,
    DateTime CreatedAt,
    Guid Id = default,
    Guid UserId = default,
    DateTime? LastModifiedAt = null,
    Guid? CreatedBy = null,
    Guid? LastModifiedBy = null,
    Guid? DeletedBy = null,
    DateTime? DeletedAt = null,
    bool IsDeleted = false);

/// <summary>
/// Versioned taste/account export; full inventory and deletion verification is a separate phase.
/// </summary>
public record UserDataExportDto(
    string ExportVersion,
    DateTime ExportedAtUtc,
    SubscriberDto Subscriber,
    IReadOnlyList<UserMusicConnectionExportDto> Connections,
    IReadOnlyList<TasteSignalDto> TasteSignals,
    IReadOnlyList<WeeklyDigestDto> Digests);
