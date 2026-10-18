using LinerNotes.Application.Common.Interfaces;
using LinerNotes.DataAccess.DataContexts;
using Microsoft.EntityFrameworkCore;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class UnsubscribeStore(DataContext db, IDiscoveryWriteCoordinator? admission = null,
    DiscoveryRuntimeOptions? runtime = null) : IUnsubscribeStore
{
    public async Task UnsubscribeAsync(Guid userId, DateTime utcNow, CancellationToken ct)
    {
        if (utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(utcNow));
        // Atomic row update coordinates with capture's final SELECT FOR UPDATE.
        Task<int> Update(CancellationToken token) => db.Users.Where(u => u.Id == userId && u.EmailUnsubscribedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailUnsubscribedAtUtc, utcNow)
                .SetProperty(u => u.LastModifiedAt, utcNow), token);
        if (runtime?.RunJobs == true)
        {
            if (admission is null) throw new InvalidOperationException("weekly_discovery_runtime_unconfigured");
            await admission.ExecuteAsync(16_384, Update, ct);
        }
        else await Update(ct);
    }
}
