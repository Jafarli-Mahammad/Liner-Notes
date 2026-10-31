using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Discovery;
using Microsoft.EntityFrameworkCore;
namespace LinerNotes.DataAccess.Persistence.Repositories;
public sealed class DiscoveryJobStore(DataContext db, DiscoveryRuntimeOptions? options = null) : IDiscoveryJobStore
{
    private readonly TimeSpan maximumAge = TimeSpan.FromSeconds((options ?? new()).MaximumJobAgeSeconds);
    private static readonly DiscoveryJobState[] Unfinished = [DiscoveryJobState.Pending, DiscoveryJobState.Running, DiscoveryJobState.Retryable];
    public Task<bool> HasReadyAsync(DiscoveryJobKind kind, DiscoveryJobLane lane, DateTimeOffset now, int maximumAttempts, CancellationToken ct)
        => db.DiscoveryJobs.AsNoTracking().AnyAsync(j => j.Kind == kind &&
            (Unfinished.Contains(j.State) && j.CreatedAtUtc <= now - maximumAge ||
            (j.Attempts < maximumAttempts || j.State == DiscoveryJobState.Running && j.LeaseExpiresAtUtc <= now) &&
            (lane == DiscoveryJobLane.Acquisition ? kind == DiscoveryJobKind.Lookup || j.ProgressCode == "waiting_for_music" :
                kind != DiscoveryJobKind.Lookup && j.ProgressCode != "waiting_for_music") &&
            db.ListenerProfiles.Any(p => p.Id == j.ProfileId && (p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)) &&
            ((j.State == DiscoveryJobState.Pending || j.State == DiscoveryJobState.Retryable) && j.NextAttemptAtUtc <= now ||
             j.State == DiscoveryJobState.Running && j.LeaseExpiresAtUtc <= now)), ct);
    public Task<DiscoveryJob?> FindAsync(Guid profileId, Guid jobId, DateTimeOffset now, CancellationToken ct) =>
        db.DiscoveryJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == jobId && j.ProfileId == profileId &&
            db.ListenerProfiles.Any(p => p.Id == profileId && (p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)), ct);
    public Task<DiscoveryJob?> FindByKeyAsync(Guid profileId, string key, CancellationToken ct) =>
        db.DiscoveryJobs.AsNoTracking().SingleOrDefaultAsync(j => j.ProfileId == profileId && j.RequestKey == key, ct);
    public async Task<DiscoveryJob> EnqueueAsync(Guid profileId, string key, string hash, DiscoveryJobKind kind, long revision,
        string payloadJson, DateTimeOffset now, int maximumPending, TimeSpan cooldown, CancellationToken ct)
    {
        RequireTransaction();
        if (maximumPending is < 1 or > 10_000 || cooldown < TimeSpan.Zero || cooldown > TimeSpan.FromMinutes(10)) throw new ArgumentException("invalid_queue_options");
        var profile = await db.ListenerProfiles.FromSqlInterpolated($"""
            SELECT * FROM "ListenerProfiles" WHERE "Id"={profileId} FOR UPDATE
            """).AsTracking().SingleOrDefaultAsync(ct) ?? throw new UnauthorizedAccessException();
        await db.Entry(profile).ReloadAsync(ct);
        if (profile.IsExpired(now)) throw new UnauthorizedAccessException();
        var existing = await FindByKeyAsync(profileId, key, ct);
        if (existing is not null)
        {
            if (existing.RequestHash != hash || existing.Kind != kind) throw new DiscoveryRequestConflictException("idempotency_key_reused");
            return existing;
        }
        if (kind == DiscoveryJobKind.Generation && profile.Revision != revision) throw new DiscoveryRequestConflictException("profile_revision_conflict");
        if (await db.DiscoveryJobs.AnyAsync(j => j.ProfileId == profileId && j.Kind == kind && Unfinished.Contains(j.State), ct))
            throw new DiscoveryBusyException("discovery_busy", TimeSpan.FromSeconds(2));
        var last = await db.DiscoveryJobs.Where(j => j.ProfileId == profileId && j.Kind == kind)
            .MaxAsync(j => (DateTimeOffset?)j.CreatedAtUtc, ct);
        if (last is { } accepted && now < accepted.Add(cooldown))
            throw new DiscoveryBusyException("discovery_cooldown", accepted.Add(cooldown) - now);
        if (await db.DiscoveryJobs.CountAsync(j => Unfinished.Contains(j.State), ct) >= maximumPending)
            throw new DiscoveryBusyException("discovery_queue_full", TimeSpan.FromSeconds(5));
        var job = new DiscoveryJob(profileId, key, hash, kind, revision, payloadJson, now);
        db.DiscoveryJobs.Add(job); await db.SaveChangesAsync(ct); return job;
    }
    public async Task<DiscoveryJob?> LeaseNextAsync(DiscoveryJobKind kind, DateTimeOffset now, TimeSpan duration, int maximumAttempts, CancellationToken ct, DiscoveryJobLane? lane = null)
    {
        RequireTransaction();
        bool anyLane = lane is null;
        bool acquisition = lane == DiscoveryJobLane.Acquisition;
        bool lookup = kind == DiscoveryJobKind.Lookup;
        string kindText = kind.ToString();
        var expired = await db.DiscoveryJobs.FromSqlInterpolated($"""
            SELECT j.* FROM "DiscoveryJobs" j WHERE j."Kind"={kindText} AND j."State" IN ('Pending','Retryable','Running')
            AND j."CreatedAtUtc"<={now - maximumAge}
            ORDER BY j."CreatedAtUtc",j."Id" LIMIT 16 FOR UPDATE SKIP LOCKED
            """).AsTracking().ToArrayAsync(ct);
        foreach (var job in expired) { await db.Entry(job).ReloadAsync(ct); job.Expire(now, maximumAge); }
        if (expired.Length > 0) await db.SaveChangesAsync(ct);
        var exhausted = await db.DiscoveryJobs.FromSqlInterpolated($"""
            SELECT j.* FROM "DiscoveryJobs" j WHERE j."Kind"={kindText} AND j."State" IN ('Pending','Retryable','Running')
            AND j."Attempts">={maximumAttempts} AND (j."LeaseExpiresAtUtc" IS NULL OR j."LeaseExpiresAtUtc"<={now})
            ORDER BY j."CreatedAtUtc",j."Id" LIMIT 16 FOR UPDATE SKIP LOCKED
            """).AsTracking().ToArrayAsync(ct);
        foreach (var job in exhausted) { await db.Entry(job).ReloadAsync(ct); job.Cancel(now, "attempts_exhausted"); }
        if (exhausted.Length > 0) await db.SaveChangesAsync(ct);
        var next = await db.DiscoveryJobs.FromSqlInterpolated($"""
            SELECT j.* FROM "DiscoveryJobs" j JOIN "ListenerProfiles" p ON p."Id"=j."ProfileId"
            WHERE j."Kind"={kindText} AND j."Attempts"<{maximumAttempts} AND j."CreatedAtUtc">{now - maximumAge}
              AND ({anyLane} OR ({acquisition} AND ({lookup} OR j."ProgressCode"='waiting_for_music')) OR
                (NOT {acquisition} AND NOT {lookup} AND j."ProgressCode"<>'waiting_for_music'))
            AND (p."ExpiresAtUtc" IS NULL OR p."ExpiresAtUtc">{now}) AND
              ((j."State" IN ('Pending','Retryable') AND j."NextAttemptAtUtc"<={now}) OR
               (j."State"='Running' AND j."LeaseExpiresAtUtc"<={now}))
            ORDER BY j."NextAttemptAtUtc",j."CreatedAtUtc",j."Id" LIMIT 1 FOR UPDATE OF j SKIP LOCKED
            """).AsTracking().SingleOrDefaultAsync(ct);
        if (next is null) return null;
        await db.Entry(next).ReloadAsync(ct);
        next.AcquireLease(Guid.NewGuid(), now, duration, maximumAttempts);
        await db.SaveChangesAsync(ct); return next;
    }
    public async Task<DiscoveryJob> LockLeasedAsync(Guid jobId, Guid token, DateTimeOffset now, CancellationToken ct)
    {
        RequireTransaction();
        var profileId = await db.DiscoveryJobs.Where(j => j.Id == jobId).Select(j => (Guid?)j.ProfileId).SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("job_lease_lost");
        // Every compound owner/job operation takes the owner lock first, matching deletion/import.
        var profile = await db.ListenerProfiles.FromSqlInterpolated($"""
            SELECT * FROM "ListenerProfiles" WHERE "Id"={profileId} FOR UPDATE
            """).AsNoTracking().SingleOrDefaultAsync(ct);
        var job = await db.DiscoveryJobs.FromSqlInterpolated($"""
            SELECT * FROM "DiscoveryJobs" WHERE "Id"={jobId} FOR UPDATE
            """).AsTracking().SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("job_lease_lost");
        await db.Entry(job).ReloadAsync(ct);
        if (now - job.CreatedAtUtc >= maximumAge) throw new InvalidOperationException("job_deadline_passed");
        job.RequireLease(token, now);
        if (job.ProfileId != profileId || profile is null || profile.IsExpired(now) ||
            (job.Kind == DiscoveryJobKind.Generation && profile.Revision != job.InputRevision))
            throw new InvalidOperationException("job_inputs_changed");
        return job;
    }
    public async Task DeferAsync(Guid jobId, Guid token, DateTimeOffset now, TimeSpan delay, string progress, CancellationToken ct)
    {
        var job = await LockLeasedAsync(jobId, token, now, ct);
        job.DeferLease(token, now, delay, progress);
        await db.SaveChangesAsync(ct);
    }
    public async Task RenewAsync(Guid jobId, Guid token, DateTimeOffset now, TimeSpan duration, string progress, CancellationToken ct)
    {
        var job = await LockLeasedAsync(jobId, token, now, ct);
        job.RenewLease(token, now, duration, progress); await db.SaveChangesAsync(ct);
    }
    public async Task FailAsync(Guid jobId, Guid token, DateTimeOffset now, string code, bool retryable, TimeSpan delay, int maximumAttempts, CancellationToken ct)
    {
        RequireTransaction();
        var job = await db.DiscoveryJobs.FromSqlInterpolated($"""
            SELECT * FROM "DiscoveryJobs" WHERE "Id"={jobId} FOR UPDATE
            """).AsTracking().SingleOrDefaultAsync(ct) ?? throw new InvalidOperationException("job_lease_lost");
        await db.Entry(job).ReloadAsync(ct);
        job.Fail(token, now, code, retryable, delay, maximumAttempts); await db.SaveChangesAsync(ct);
    }
    public async Task SaveCompletedAsync(DiscoveryJob job, Guid token, DateTimeOffset now, Guid? batchId, string? resultJson, CancellationToken ct)
    {
        RequireTransaction();
        var current = await LockLeasedAsync(job.Id, token, now, ct);
        current.Complete(token, now, batchId, resultJson); await db.SaveChangesAsync(ct);
    }
    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("job_write_requires_coordinated_transaction");
    }
}
