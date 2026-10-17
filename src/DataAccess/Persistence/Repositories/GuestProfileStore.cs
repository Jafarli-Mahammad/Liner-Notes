using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.DTOs.Export;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Discovery;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class GuestProfileStore(DataContext db) : IGuestProfileStore
{
    public Task<ListenerProfile?> FindGuestAsync(string hash, DateTimeOffset now, CancellationToken ct) =>
        db.ListenerProfiles.AsNoTracking().Where(p => p.UserId == null && p.ExpiresAtUtc > now &&
            db.GuestSessions.Any(s => s.ProfileId == p.Id && s.CredentialHash == hash)).SingleOrDefaultAsync(ct);

    public Task<ListenerProfile?> FindAccountAsync(Guid userId, CancellationToken ct) =>
        db.ListenerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId && db.Users.Any(u => u.Id == userId), ct);

    public async Task<ListenerProfile> CreateAsync(string hash, DateTimeOffset now, TimeSpan inactivity, CancellationToken ct)
    {
        RequireTransaction();
        var profile = ListenerProfile.Guest(now, inactivity);
        db.ListenerProfiles.Add(profile);
        db.GuestSessions.Add(new GuestSession(profile.Id, hash, now));
        await db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task TouchAsync(Guid profileId, DateTimeOffset now, TimeSpan inactivity, TimeSpan minimumInterval, CancellationToken ct)
    {
        RequireTransaction();
        var profile = await db.ListenerProfiles.FromSqlInterpolated($"""
            SELECT * FROM "ListenerProfiles" WHERE "Id"={profileId} AND "UserId" IS NULL AND "ExpiresAtUtc">{now} FOR UPDATE
            """).AsTracking().SingleOrDefaultAsync(ct) ?? throw new UnauthorizedAccessException();
        if (profile.Touch(now, inactivity, minimumInterval)) await db.SaveChangesAsync(ct);
    }

    public Task<DiscoveryExportDto> ExportAsync(Guid profileId, CancellationToken ct) =>
        new DiscoveryProfileReader(db).GetByProfileAsync(profileId, ct);

    public async Task DeleteGuestAsync(string hash, DateTimeOffset now, CancellationToken ct)
    {
        RequireTransaction();
        var profile = await LockGuestAsync(hash, now, ct) ?? throw new UnauthorizedAccessException();
        await db.ListenerProfiles.Where(p => p.Id == profile.Id && p.UserId == null).ExecuteDeleteAsync(ct);
    }

    public async Task AttachNewAccountAsync(string hash, Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        RequireTransaction();
        var profile = await LockGuestAsync(hash, now, ct) ?? throw new UnauthorizedAccessException();
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct) || await db.ListenerProfiles.AnyAsync(p => p.UserId == userId, ct))
            throw new InvalidOperationException("account_profile_already_exists");
        profile.Attach(userId, now);
        await db.GuestSessions.Where(s => s.ProfileId == profile.Id).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task ImportIntoAccountAsync(string hash, Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        RequireTransaction();
        // All new writers use the same short admission lock. Row locks also fence deletion and attachment.
        var source = await LockGuestAsync(hash, now, ct) ?? throw new UnauthorizedAccessException();
        var target = await db.ListenerProfiles.FromSqlInterpolated($"""
            SELECT * FROM "ListenerProfiles" WHERE "UserId"={userId} FOR UPDATE
            """).AsTracking().SingleOrDefaultAsync(ct);
        if (target is null || !await db.Users.AnyAsync(u => u.Id == userId, ct)) throw new UnauthorizedAccessException();
        await db.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL DEFERRED", ct);
        // Explicit import retains history, cancels unfinished work, and assigns stable collision-free request keys.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "DiscoveryJobs" SET "State"='Cancelled', "ProgressCode"='cancelled', "ErrorCode"='profile_imported',
                "PayloadJson"=jsonb_build_object(), "LeaseToken"=NULL, "LeaseExpiresAtUtc"=NULL, "UpdatedAtUtc"={now}
            WHERE "ProfileId" IN ({source.Id},{target.Id}) AND "State" IN ('Pending','Running','Retryable');
            DELETE FROM "TasteSignals" s USING "TasteSignals" t
            WHERE s."ProfileId"={source.Id} AND t."ProfileId"={target.Id}
              AND s."Source"='InitialSeedManual' AND t."Source"=s."Source"
              AND t."TargetType"=s."TargetType" AND
                  ((s."CanonicalEntityId" IS NOT NULL AND t."CanonicalEntityId"=s."CanonicalEntityId") OR
                   (s."CanonicalEntityId" IS NULL AND t."CanonicalEntityId" IS NULL AND t."NormalizedTargetValue"=s."NormalizedTargetValue"));
            UPDATE "TasteSignals" SET "ProfileId"={target.Id},"UserId"={userId} WHERE "ProfileId"={source.Id};
            UPDATE "DiscoveryBatches" SET "ProfileId"={target.Id},"RequestId"='import:' || replace("Id"::text,'-','') WHERE "ProfileId"={source.Id};
            UPDATE "DiscoveryJobs" SET "ProfileId"={target.Id},"RequestKey"='import:' || replace("Id"::text,'-','') WHERE "ProfileId"={source.Id};
            UPDATE "WeeklyRecommendations" SET "ProfileId"={target.Id},"UserId"={userId} WHERE "ProfileId"={source.Id};
            """, ct);
        target.ChangeInputs(target.Revision);
        await db.SaveChangesAsync(ct);
        await db.ListenerProfiles.Where(p => p.Id == source.Id && p.UserId == null).ExecuteDeleteAsync(ct);
        db.Entry(source).State = EntityState.Detached;
    }

    public async Task<int> DeleteExpiredAsync(DateTimeOffset now, int limit, CancellationToken ct)
    {
        RequireTransaction();
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var ids = await db.ListenerProfiles.FromSqlInterpolated($"""
            SELECT * FROM "ListenerProfiles" WHERE "UserId" IS NULL AND "ExpiresAtUtc"<={now}
            ORDER BY "ExpiresAtUtc","Id" LIMIT {limit} FOR UPDATE SKIP LOCKED
            """).Select(p => p.Id).ToArrayAsync(ct);
        return await db.ListenerProfiles.Where(p => ids.Contains(p.Id) && p.UserId == null && p.ExpiresAtUtc <= now).ExecuteDeleteAsync(ct);
    }

    private Task<ListenerProfile?> LockGuestAsync(string hash, DateTimeOffset now, CancellationToken ct) =>
        db.ListenerProfiles.FromSqlInterpolated($"""
            SELECT p.* FROM "ListenerProfiles" p JOIN "GuestSessions" s ON s."ProfileId"=p."Id"
            WHERE s."CredentialHash"={hash} AND p."UserId" IS NULL AND p."ExpiresAtUtc">{now} FOR UPDATE OF p
            """).AsTracking().SingleOrDefaultAsync(ct);

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("guest_write_requires_coordinated_transaction");
    }
}
