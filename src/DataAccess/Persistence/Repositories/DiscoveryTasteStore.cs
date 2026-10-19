using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;
using Microsoft.EntityFrameworkCore;
namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class DiscoveryTasteStore(DataContext db) : IDiscoveryTasteStore, LinerNotes.Application.Features.Discovery.ITasteOnboardingStore
{
    public async Task<long> SaveAsync(Guid profileId, long revision, LinerNotes.Domain.Discovery.TasteOnboarding answers,
        IReadOnlyList<TasteSignal> selections, DateTimeOffset now, CancellationToken ct)
    {
        answers.Validate();
        long next = await ReplaceManualAsync(profileId, revision, selections, now, ct);
        var profile = db.ListenerProfiles.Local.Single(p => p.Id == profileId);
        profile.SetOnboarding(System.Text.Json.JsonSerializer.Serialize(answers));
        await db.SaveChangesAsync(ct);
        return next;
    }
    public async Task<long> ReplaceManualAsync(Guid profileId, long expectedRevision, IReadOnlyList<TasteSignal> signals,
        DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("taste_write_requires_coordinated_transaction");
        if (signals.Any(s => s.ProfileId != profileId || s.Source != TasteSignalSource.InitialSeedManual))
            throw new ArgumentException("invalid_discovery_selections");
        var profile = await db.ListenerProfiles.FromSqlInterpolated($"""
            SELECT * FROM "ListenerProfiles" WHERE "Id"={profileId} FOR UPDATE
            """).AsTracking().SingleOrDefaultAsync(ct) ?? throw new UnauthorizedAccessException();
        await db.Entry(profile).ReloadAsync(ct);
        if (profile.IsExpired(now)) throw new UnauthorizedAccessException();
        profile.ChangeInputs(expectedRevision);
        var pending = await db.DiscoveryJobs.AsTracking().Where(j => j.ProfileId == profileId &&
            j.Kind == LinerNotes.Domain.Discovery.DiscoveryJobKind.Generation &&
            (j.State == LinerNotes.Domain.Discovery.DiscoveryJobState.Pending || j.State == LinerNotes.Domain.Discovery.DiscoveryJobState.Running ||
             j.State == LinerNotes.Domain.Discovery.DiscoveryJobState.Retryable)).ToArrayAsync(ct);
        foreach (var job in pending) job.Cancel(now, "taste_changed");
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
