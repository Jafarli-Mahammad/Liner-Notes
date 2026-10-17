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
    public async Task<DiscoveryInputs?> ReadInputsAsync(Guid profileId, DateTimeOffset now, int feedbackWindow, CancellationToken ct)
    {
        if (feedbackWindow is < 1 or > 40) throw new ArgumentOutOfRangeException(nameof(feedbackWindow));
        var profile = await db.ListenerProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == profileId &&
            (p.ExpiresAtUtc == null || p.ExpiresAtUtc > now), ct);
        if (profile is null) return null;
        var signals = await db.TasteSignals.AsNoTracking().Where(s => s.ProfileId == profileId &&
            (s.TargetType == TasteTargetType.Artist || s.TargetType == TasteTargetType.Tag || s.TargetType == TasteTargetType.Album) &&
            s.Source != TasteSignalSource.RecommendationLike && s.Source != TasteSignalSource.RecommendationDislike &&
            s.Source != TasteSignalSource.RecommendationAlreadyKnown && s.Source != TasteSignalSource.RecommendationRating)
            .OrderBy(s => s.Id).Take(201).ToArrayAsync(ct);
        if (signals.Length > 200) throw new GenerationStoppedException("taste_evidence_limit");
        var feedback = await db.WeeklyRecommendations.AsNoTracking().Include(r => r.Track)
            .Where(r => r.ProfileId == profileId && r.FeedbackGivenAt != null)
            .OrderByDescending(r => r.FeedbackGivenAt).ThenByDescending(r => r.Id).Take(feedbackWindow).ToArrayAsync(ct);
        return new(profile, signals, feedback);
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
    }
}
