using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.Application.Common.Interfaces.Discovery;

namespace LinerNotes.Application.Features.Discovery;

public sealed class CleanupPublicDiscovery(IPublicEvidenceStore evidence, ICatalogMaintenance catalog,
    IDiscoveryWriteCoordinator admission, TimeProvider clock)
{
    public Task<int> ExecuteAsync(CancellationToken ct) => admission.ReclaimAsync(async token =>
    {
        var now = clock.GetUtcNow();
        int removed = await evidence.DeleteExpiredAsync(now, 100, token);
        return removed + await catalog.DeleteExpiredAsync(now, 100, token);
    }, ct);
}
