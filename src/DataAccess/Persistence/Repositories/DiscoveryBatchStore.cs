using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class DiscoveryBatchStore(DataContext db, IDiscoveryJobStore jobs, TimeProvider clock) : IDiscoveryBatchStore
{
    public async Task<long> SaveFeedbackAsync(Guid profileId, Guid recommendationId, long expectedRevision,
        UserFeedback feedback, int? rating, string? comment, DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("feedback_requires_coordinated_transaction");
        var profile = await db.ListenerProfiles.FromSqlInterpolated($"""
            SELECT * FROM "ListenerProfiles" WHERE "Id"={profileId} FOR UPDATE
            """).AsTracking().SingleOrDefaultAsync(ct) ?? throw new UnauthorizedAccessException();
        await db.Entry(profile).ReloadAsync(ct);
        if (profile.IsExpired(now)) throw new UnauthorizedAccessException();
        var pick = await db.WeeklyRecommendations.FromSqlInterpolated($"""
            SELECT * FROM "WeeklyRecommendations" WHERE "Id"={recommendationId} AND "ProfileId"={profileId} FOR UPDATE
            """).AsTracking().Include(r => r.Track).SingleOrDefaultAsync(ct)
            ?? throw new LinerNotes.Application.Common.Exceptions.NotFoundException("recommendation", recommendationId);
        await db.Entry(pick).ReloadAsync(ct);
        if (pick.Feedback == feedback && pick.Rating == rating && pick.FeedbackComment == comment) return profile.Revision;
        if (profile.Revision != expectedRevision) throw new DiscoveryRequestConflictException("profile_revision_conflict");
        bool retainKnown = !(feedback == UserFeedback.None && rating is null) && (feedback == UserFeedback.AlreadyKnown || pick.Feedback == UserFeedback.AlreadyKnown);
        string prefix = "rec:" + recommendationId;
        var previous = await db.TasteSignals.AsTracking().Where(t => t.ProfileId == profileId && t.Context.StartsWith(prefix) &&
            (t.Source == TasteSignalSource.RecommendationLike || t.Source == TasteSignalSource.RecommendationDislike ||
             t.Source == TasteSignalSource.RecommendationAlreadyKnown || t.Source == TasteSignalSource.RecommendationRating)).ToArrayAsync(ct);
        retainKnown |= !(feedback == UserFeedback.None && rating is null) && previous.Any(t => t.Source == TasteSignalSource.RecommendationAlreadyKnown);
        db.TasteSignals.RemoveRange(previous);
        pick.RecordFeedback(feedback, comment, rating);
        if (retainKnown)
            db.TasteSignals.Add(LinerNotes.Domain.Taste.TasteSignal.ForProfile(profileId, profile.UserId, TasteTargetType.Track,
                pick.Track.TrackKey, 0, TasteSignalSource.RecommendationAlreadyKnown, prefix + " - Marked familiar."));
        profile.ChangeInputs(expectedRevision);
        var pending = await db.DiscoveryJobs.AsTracking().Where(j => j.ProfileId == profileId && j.Kind == DiscoveryJobKind.Generation &&
            (j.State == DiscoveryJobState.Pending || j.State == DiscoveryJobState.Running || j.State == DiscoveryJobState.Retryable)).ToArrayAsync(ct);
        foreach (var job in pending) job.Cancel(now, "feedback_changed");
        await db.SaveChangesAsync(ct);
        return profile.Revision;
    }

    public async Task<DiscoveryInputs?> ReadInputsAsync(Guid profileId, DateTimeOffset now, int feedbackWindow, CancellationToken ct)
    {
        if (feedbackWindow is < 1 or > 40) throw new ArgumentOutOfRangeException(nameof(feedbackWindow));
        var profile = await db.ListenerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == profileId &&
            (p.ExpiresAtUtc == null || p.ExpiresAtUtc > now), ct);
        if (profile is null) return null;
        var signals = await db.TasteSignals.AsNoTracking().Where(s => s.ProfileId == profileId &&
            (s.TargetType == TasteTargetType.Artist || s.TargetType == TasteTargetType.Tag || s.TargetType == TasteTargetType.Album || s.TargetType == TasteTargetType.Track) &&
            s.Source != TasteSignalSource.RecommendationLike && s.Source != TasteSignalSource.RecommendationDislike &&
            s.Source != TasteSignalSource.RecommendationAlreadyKnown && s.Source != TasteSignalSource.RecommendationRating)
            .OrderBy(s => s.Id).ToArrayAsync(ct);
        var feedback = await db.WeeklyRecommendations.AsNoTracking().Include(r => r.Track)
            .Where(r => r.ProfileId == profileId && r.FeedbackGivenAt != null)
            .OrderByDescending(r => r.FeedbackGivenAt).ThenByDescending(r => r.Id).Take(feedbackWindow).ToArrayAsync(ct);
        return new(profile, signals, feedback, await db.DiscoveryBatches.LongCountAsync(b => b.ProfileId == profileId && b.CompletedAtUtc != null, ct));
    }

    public async Task<IReadOnlySet<string>> FindExcludedAsync(Guid profileId, IReadOnlyList<string> aliases, CancellationToken ct)
    {
        if (aliases.Count > 800) throw new ArgumentException("candidate_alias_limit");
        var history = await db.Set<LinerNotes.DataAccess.Persistence.Accounting.RecommendedIdentity>().AsNoTracking()
            .Where(h => h.ProfileId == profileId && aliases.Contains(h.Alias)).Select(h => h.Alias).ToArrayAsync(ct);
        var explicitExclusions = await db.TasteSignals.AsNoTracking().Where(s => s.ProfileId == profileId &&
            s.TargetType == TasteTargetType.Track && s.Weight < 0 && aliases.Contains(s.NormalizedTargetValue))
            .Select(s => s.NormalizedTargetValue).ToArrayAsync(ct);
        return history.Concat(explicitExclusions).ToHashSet(StringComparer.Ordinal);
    }

    public async Task CompleteAsync(Guid jobId, Guid leaseToken, Guid batchId, DiscoveryBatchKind kind,
        IReadOnlyList<BaselineAPick> picks, string coverageJson, DateTimeOffset? evidenceExpiry,
        DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("batch_write_requires_coordinated_transaction");
        if (picks.Count > 5 || evidenceExpiry <= now) throw new GenerationStoppedException("prepared_evidence_expired");
        var job = await jobs.LockLeasedAsync(jobId, leaseToken, now, ct);
        if (job.Kind != DiscoveryJobKind.Generation) throw new InvalidOperationException("generation_job_required");
        var profile = await db.ListenerProfiles.AsNoTracking().SingleAsync(p => p.Id == job.ProfileId, ct);
        var aliases = picks.SelectMany(p => TrackIdentity.Aliases(p.Track)).Distinct(StringComparer.Ordinal).ToArray();
        if ((await FindExcludedAsync(job.ProfileId, aliases, ct)).Count != 0)
            throw new InvalidOperationException("recommendation_history_changed");
        var batch = new DiscoveryBatch(job.ProfileId, job.RequestKey, kind, job.InputRevision, now, batchId);
        batch.RecordEvidence(null, "prepared-public-v1", coverageJson);
        batch.Complete(now);
        db.DiscoveryBatches.Add(batch);
        int rank = 0;
        foreach (var pick in picks)
        {
            if (pick.Breakdown.Snapshot?.DiscoveryBatchId != batchId) throw new InvalidOperationException("batch_score_identity_mismatch");
            var candidate = pick.Track;
            var track = await db.Tracks.AsTracking().Where(t => t.NormalizedArtistName == candidate.NormalizedArtistName &&
                t.NormalizedTitle == candidate.NormalizedTitle).OrderBy(t => t.Id).FirstOrDefaultAsync(ct);
            if (track is null) { track = candidate; db.Tracks.Add(track); }
            db.WeeklyRecommendations.Add(WeeklyRecommendation.ForDiscovery(job.ProfileId, batchId, profile.UserId,
                track, ++rank, pick.Breakdown));
        }
        await db.SaveChangesAsync(ct);
        var completedAt = clock.GetUtcNow();
        if (evidenceExpiry <= completedAt) throw new GenerationStoppedException("prepared_evidence_expired");
        await jobs.SaveCompletedAsync(job, leaseToken, completedAt, batchId, null, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "RecentRecommendations" WHERE "MetadataExpiresAtUtc" <= {completedAt}
            """, ct);
        // Publish public catalog metadata only, in the successful completion transaction.
        foreach (var recordingId in picks.Select(p => Guid.TryParse(p.Track.Mbid, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct())
        {
            var metadata = await (from r in db.CatalogRecords.AsNoTracking()
                                  join publication in db.CatalogPublications.AsNoTracking() on r.PublicationId equals publication.Id
                                  where r.Kind == LinerNotes.Domain.Catalog.CatalogKind.Track && r.CanonicalId == recordingId &&
                                      publication.State == LinerNotes.Domain.Catalog.CatalogPublicationState.Published &&
                                      (publication.IsLookup || db.CatalogHeads.Any(h => h.PublicationId == publication.Id)) &&
                                      (publication.ExpiresAtUtc == null || publication.ExpiresAtUtc > completedAt) &&
                                      (publication.Origin == LinerNotes.Domain.Catalog.CatalogOrigin.Synthetic || publication.MetadataLicense == "CC0-1.0")
                                  orderby publication.AcquiredAtUtc descending
                                  select new { r.DisplayName, r.ArtistDisplayName, publication.Origin, publication.Attribution, publication.ExpiresAtUtc }).FirstOrDefaultAsync(ct);
            if (metadata?.ArtistDisplayName is not { Length: > 0 } artistName) continue;
            if (await db.Set<RecentRecommendation>().AnyAsync(r => r.RecordingId == recordingId, ct)) continue;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "RecentRecommendations" ("RecordingId","Title","ArtistName","Origin","Attribution","MetadataExpiresAtUtc")
                VALUES ({recordingId},{metadata.DisplayName},{artistName},{metadata.Origin.ToString()},{metadata.Attribution},{metadata.ExpiresAtUtc})
                ON CONFLICT ("RecordingId") DO NOTHING
                """, ct);
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "RecentRecommendations" WHERE "MetadataExpiresAtUtc" <= {completedAt}
                OR "Sequence" IN (SELECT "Sequence" FROM "RecentRecommendations" ORDER BY "Sequence" DESC OFFSET 1000)
            """, ct);

    }
}
