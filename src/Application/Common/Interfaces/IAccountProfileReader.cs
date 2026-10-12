using LinerNotes.Application.DTOs.Export;

namespace LinerNotes.Application.Common.Interfaces;

public interface IAccountProfileReader
{
    /// <summary>Returns stored account profile fields, excluding credentials, or null for a missing or deleted Identity account.</summary>
    Task<AccountExportDto?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}
