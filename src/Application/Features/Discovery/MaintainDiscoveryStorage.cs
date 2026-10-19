using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;

namespace LinerNotes.Application.Features.Discovery;

public sealed class MaintainDiscoveryStorage(IDigestGenerationStore inventory, ICoordinatedGenerationStorage storage)
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        await using var lease = await inventory.AcquireBatchAsync(ct);
        await storage.RefreshKnownInventoryAsync(await inventory.ReadStorageAsync(ct), ct);
    }
}
