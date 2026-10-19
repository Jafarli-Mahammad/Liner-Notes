using LinerNotes.Application.DTOs.Export;

namespace LinerNotes.Application.Common.Interfaces.Discovery;

public interface IDiscoveryProfileReader
{
    Task<DiscoveryExportDto> GetForAccountAsync(Guid userId, CancellationToken cancellationToken = default);
}
