namespace LinerNotes.Application.DTOs.Export;

/// <summary>Owned file lifecycle metadata. Deployment root configuration is global operational state.</summary>
public sealed record ArtifactPublicationExportDto(Guid Id, Guid? OwnerId, string Category, string FinalName,
    string StageName, long ReservedBytes, long ContentBytes, string Sha256, string Phase, long Version, long LastWriteTimeUtcTicks);
