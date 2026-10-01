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
    DateTime CreatedAt);

/// <summary>
/// Full user data export model fulfilling GDPR transparency and data portability requirements.
/// </summary>
public record UserDataExportDto(
    string ExportVersion,
    DateTime ExportedAtUtc,
    SubscriberDto Subscriber,
    IReadOnlyList<UserMusicConnectionExportDto> Connections,
    IReadOnlyList<TasteSignalDto> TasteSignals,
    IReadOnlyList<WeeklyDigestDto> Digests);
