using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;

namespace LinerNotes.Infrastructure.Discovery;

public sealed class PersistentDiscoveryPreparation(SharedDiscoveryPreparation memory, SharedPublicEvidence evidence)
    : IDiscoveryPreparation
{
    public Task<PreparedDiscoveryPool> GetAsync(DiscoveryPoolRequest request, CancellationToken ct)
        => GetAsync(request, true, ct);

    public async Task<PreparedDiscoveryPool> GetAsync(DiscoveryPoolRequest request, bool allowAcquisition, CancellationToken ct)
    {
        var hit = memory.TryGet(request);
        if (hit is not null) return hit;
        var document = await evidence.GetAsync(memory.KeyFor(request), PublicEvidenceKind.Pool,
            PublicEvidenceCodec.PoolVersion, allowAcquisition,
            async token => PublicEvidenceCodec.Encode(await memory.GetAsync(request, token)), ct);
        var pool = PublicEvidenceCodec.DecodePool(document);
        memory.Remember(pool);
        return pool;
    }
}
