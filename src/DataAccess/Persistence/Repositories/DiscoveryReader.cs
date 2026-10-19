using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class DiscoveryReader(DataContext db) : IDiscoveryReader
{
    public async Task<DiscoveryPage?> ReadAsync(Guid profileId, DateTimeOffset now, TimeSpan cooldown, CancellationToken ct)
    {
        var revision = await db.ListenerProfiles.AsNoTracking().Where(p => p.Id == profileId &&
            (p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)).Select(p => (long?)p.Revision).SingleOrDefaultAsync(ct);
        if (revision is null) return null;
        var signals = await db.TasteSignals.AsNoTracking().Where(s => s.ProfileId == profileId && s.Source == TasteSignalSource.InitialSeedManual)
            .OrderBy(s => s.Id).ToArrayAsync(ct);
        var favorites = signals.Where(s => s.TargetType is TasteTargetType.Artist or TasteTargetType.Album or TasteTargetType.Track).Select(s =>
        {
            var selected = s.SelectionJson is null ? null : JsonSerializer.Deserialize<CatalogSelectionSnapshot>(s.SelectionJson);
            return new DiscoveryFavorite(s.CatalogRecordId, s.TargetValue, selected?.Item.ArtistName, s.TargetType.ToString(), selected?.Item.Disambiguation, selected?.Item.Releases, s.Id);
        }).ToArray();
        var batches = await db.DiscoveryBatches.AsNoTracking().Where(b => b.ProfileId == profileId && b.CompletedAtUtc != null)
            .OrderByDescending(b => b.CreatedAtUtc).ThenByDescending(b => b.Id).Take(10)
            .Select(b => new DiscoveryBatchSummary(b.Id, b.Kind.ToString(), b.CreatedAtUtc, b.CompletedAtUtc)).ToArrayAsync(ct);
        var job = await db.DiscoveryJobs.AsNoTracking().Where(j => j.ProfileId == profileId && j.Kind == DiscoveryJobKind.Generation)
            .OrderByDescending(j => j.CreatedAtUtc).ThenByDescending(j => j.Id).FirstOrDefaultAsync(ct);
        return new(revision.Value, favorites, signals.Where(s => s.TargetType == TasteTargetType.Tag).Select(s => s.NormalizedTargetValue).ToArray(),
            batches, job is null ? null : DiscoveryJobStatus.From(job), job?.CreatedAtUtc.Add(cooldown),
            OnboardingState.Read(await db.ListenerProfiles.Where(p => p.Id == profileId).Select(p => p.OnboardingJson).SingleAsync(ct)));
    }

    public async Task<DiscoveryBatchView?> ReadBatchAsync(Guid profileId, Guid batchId, DateTimeOffset now, CancellationToken ct)
    {
        var batch = await db.DiscoveryBatches.AsNoTracking().SingleOrDefaultAsync(b => b.ProfileId == profileId && b.Id == batchId &&
            db.ListenerProfiles.Any(p => p.Id == profileId && (p.ExpiresAtUtc == null || p.ExpiresAtUtc > now)), ct);
        if (batch is null) return null;
        var picks = await db.WeeklyRecommendations.AsNoTracking().Include(r => r.Track).Where(r => r.ProfileId == profileId && r.BatchId == batchId)
            .OrderBy(r => r.Rank).Take(5).ToArrayAsync(ct);
        JsonElement? coverage = batch.CoverageJson is null ? null : JsonSerializer.Deserialize<JsonElement>(batch.CoverageJson);
        return new(batch.Id, batch.Kind.ToString(), batch.CreatedAtUtc, coverage, picks.Select(r => new DiscoveryPick(r.Id,
            r.Track.Title, r.Track.ArtistName, r.Track.AlbumTitle, r.Rank, r.ScoreBreakdown.GenerateExplanation(),
            r.ScoreBreakdown.MatchedTags.OrderByDescending(t => t.ContributionProduct).ThenBy(t => t.TagName, StringComparer.Ordinal)
                .Take(3).Select(t => t.TagName).ToArray(), r.Feedback, r.Rating, r.FeedbackComment, r.Track.Mbid,
            r.Track.ExternalSpotifyUrl, r.Track.ExternalYoutubeUrl,
            r.ScoreBreakdown.Snapshot?.Evidence.DiscoveryPaths.Select(p => p.TrackResponse.Provider).Distinct(StringComparer.Ordinal).ToArray() ?? [])).ToArray());
    }
}
