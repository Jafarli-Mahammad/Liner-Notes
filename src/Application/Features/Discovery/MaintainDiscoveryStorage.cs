using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;

namespace LinerNotes.Application.Features.Discovery;

public sealed class MaintainDiscoveryStorage(IDigestGenerationStore inventory, ICoordinatedGenerationStorage storage)
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        await using var verified = await storage.PrepareReclamationAsync(ct);
        await using var lease = await inventory.AcquireBatchAsync(ct);
        var state = await inventory.ReadStorageAsync(ct);
        await verified.RebaseAsync(state, ct);
        await verified.PublishCommittedInventoryAsync(state, ct);
    }
}
