using LinerNotes.Application.Features.Discovery;
namespace LinerNotes.Application.Common.Interfaces.Discovery;
public interface IDiscoveryReader
{
    Task<DiscoveryPage?> ReadAsync(Guid profileId, DateTimeOffset now, TimeSpan cooldown, CancellationToken ct);
    Task<DiscoveryBatchView?> ReadBatchAsync(Guid profileId, Guid batchId, DateTimeOffset now, CancellationToken ct);
}
