using LinerNotes.Application.DTOs.Export;

namespace LinerNotes.Application.Common.Interfaces;

public interface IAccountProfileReader
{
    Task<AccountExportDto?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}
