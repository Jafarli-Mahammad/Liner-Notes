namespace LinerNotes.Application.DTOs.Export;

/// <summary>Stored Identity profile values; credential and authentication internals are excluded.</summary>
public sealed record AccountExportDto(
    Guid Id,
    string? UserName,
    string? NormalizedUserName,
    string? Email,
    string? NormalizedEmail,
    bool EmailConfirmed,
    string? PhoneNumber,
    bool PhoneNumberConfirmed,
    DateTime CreatedAt,
    Guid? CreatedBy,
    DateTime? LastModifiedAt,
    Guid? LastModifiedBy,
    DateTime? DeletedAt,
    Guid? DeletedBy,
    bool IsDeleted);
