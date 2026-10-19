using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Discovery;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class ListenerProfileRepository(DataContext db, TimeProvider clock) : IListenerProfileRepository
{
    public async Task ChangeInputsAsync(Guid userId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("profile_change_requires_transaction");
        var profile = await db.ListenerProfiles.FromSqlInterpolated($"""
            SELECT * FROM "ListenerProfiles" WHERE "UserId"={userId} FOR UPDATE
            """).AsTracking().SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("listener_profile_missing");
        await db.Entry(profile).ReloadAsync(ct);
        profile.ChangeInputs(profile.Revision);
        var jobs = await db.DiscoveryJobs.AsTracking().Where(j => j.ProfileId == profile.Id &&
            j.Kind == DiscoveryJobKind.Generation && (j.State == DiscoveryJobState.Pending ||
            j.State == DiscoveryJobState.Running || j.State == DiscoveryJobState.Retryable)).ToArrayAsync(ct);
        foreach (var job in jobs) job.Cancel(clock.GetUtcNow(), "inputs_changed");
    }
}
