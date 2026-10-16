using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;

namespace LinerNotes.DataAccess.Persistence.Repositories;

/// <summary>Fail closed until the coordinated accounting migration and implementation are installed.</summary>
public sealed class UnavailableDiscoveryWriteCoordinator : IDiscoveryWriteCoordinator
{
    public Task<T> ExecuteAsync<T>(long estimatedBytes, Func<CancellationToken, Task<T>> write, CancellationToken ct)
        => throw new GenerationStoppedException("discovery_storage_not_ready");
}
