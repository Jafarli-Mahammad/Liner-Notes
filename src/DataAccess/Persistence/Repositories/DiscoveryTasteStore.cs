using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;
using Microsoft.EntityFrameworkCore;
namespace LinerNotes.DataAccess.Persistence.Repositories;
public sealed class DiscoveryTasteStore(DataContext db) : IDiscoveryTasteStore
{
    public async Task<long> ReplaceManualAsync(Guid profileId, long expectedRevision, IReadOnlyList<TasteSignal> signals,
        DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("taste_write_requires_coordinated_transaction");
        if (signals.Count is < 1 or > 10 || signals.Any(s => s.ProfileId != profileId || s.Source != TasteSignalSource.InitialSeedManual))
            throw new ArgumentException("invalid_discovery_selections");
        var profile = await db.ListenerProfiles.FromSqlInterpolated($"""
            SELECT * FROM "ListenerProfiles" WHERE "Id"={profileId} FOR UPDATE
            """).AsTracking().SingleOrDefaultAsync(ct) ?? throw new UnauthorizedAccessException();
        await db.Entry(profile).ReloadAsync(ct);
        if (profile.IsExpired(now)) throw new UnauthorizedAccessException();
        if (await db.DiscoveryJobs.AnyAsync(j => j.ProfileId == profileId && j.Kind == LinerNotes.Domain.Discovery.DiscoveryJobKind.Generation &&
            (j.State == LinerNotes.Domain.Discovery.DiscoveryJobState.Pending || j.State == LinerNotes.Domain.Discovery.DiscoveryJobState.Running ||
             j.State == LinerNotes.Domain.Discovery.DiscoveryJobState.Retryable), ct))
            throw new InvalidOperationException("discovery_generation_busy");
        profile.ChangeInputs(expectedRevision);
        await db.TasteSignals.Where(s => s.ProfileId == profileId && s.Source == TasteSignalSource.InitialSeedManual).ExecuteDeleteAsync(ct);
        foreach (var signal in signals)
        {
            if (profile.UserId is { } userId) signal.AttachRegisteredOwner(userId, profileId);
            db.TasteSignals.Add(signal);
        }
        await db.SaveChangesAsync(ct);
        return profile.Revision;
    }
}
