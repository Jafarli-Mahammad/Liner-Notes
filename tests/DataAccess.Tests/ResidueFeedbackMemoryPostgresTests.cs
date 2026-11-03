using System.Diagnostics;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class DiscoveryCoordinationPostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task ResidueFeedbackMemoryRetainsOldArtistBeyondRecentWindowAndNoteEditsDoNotChangeRevision()
    {
        var profile = await GuestAsync('a');
        var other = await GuestAsync('b');
        var now = DateTimeOffset.UtcNow;
        var old = await WriteAsync(async (s, ct) =>
        {
            var db = s.GetRequiredService<DataContext>();
            var batch = new DiscoveryBatch(profile.Id, "durable-feedback", DiscoveryBatchKind.Initial, 0, now);
            batch.Complete(now); db.DiscoveryBatches.Add(batch);
            WeeklyRecommendation? first = null;
            for (var i = 0; i < 41; i++)
            {
                var pick = WeeklyRecommendation.ForDiscovery(profile.Id, batch.Id, null,
                    new Track("Distinct recording " + i, i == 0 ? "Established rock" : "Recent jazz"), i + 1, new ScoreBreakdown());
                pick.RecordFeedback(UserFeedback.Liked, now: now.UtcDateTime.AddMinutes(i - 50));
                db.WeeklyRecommendations.Add(pick); first ??= pick;
            }
            await db.SaveChangesAsync(ct); return first!;
        });
        async Task<DiscoveryInputs> Read(Guid id)
        {
            await using var scope = services.CreateAsyncScope();
            return (await scope.ServiceProvider.GetRequiredService<IDiscoveryBatchStore>().ReadInputsAsync(id, now, 20, default))!;
        }
        var before = await Read(profile.Id);
        Assert.Equal(20, before.Feedback.Count);
        Assert.Equal(41, before.FeedbackHistory!.Count);
        Assert.DoesNotContain(before.Feedback, r => r.Id == old.Id);
        var effective = EffectiveTasteInputs.FromProfile(profile.Id, before.Signals, before.Feedback, new(), before.FeedbackHistory);
        Assert.Contains("established rock", effective.FamiliarArtists);
        Assert.Contains("recent jazz", effective.FamiliarArtists);
        var pending = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id,
            "note-pending", new string('a', 64), DiscoveryJobKind.Generation, before.Profile.Revision, "{}", now, 10, TimeSpan.Zero, ct));
        var revision = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryBatchStore>().SaveFeedbackAsync(profile.Id,
            old.Id, before.Profile.Revision, UserFeedback.Liked, null, "Private note; never interpret", now, ct));
        Assert.Equal(before.Profile.Revision, revision);
        var after = await Read(profile.Id);
        Assert.Equal(before.FeedbackHistory!.OrderBy(r => r.Id), after.FeedbackHistory!.OrderBy(r => r.Id));
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            Assert.Equal(DiscoveryJobState.Pending, (await db.DiscoveryJobs.SingleAsync(j => j.Id == pending.Id)).State);
            var stored = await db.WeeklyRecommendations.SingleAsync(r => r.Id == old.Id);
            Assert.Equal(old.FeedbackGivenAt!.Value.Ticks / 10, stored.FeedbackGivenAt!.Value.Ticks / 10);
            Assert.True(stored.LastModifiedAt >= now.UtcDateTime);
            var exported = await scope.ServiceProvider.GetRequiredService<LinerNotes.Application.Features.Discovery.GuestLifecycle>().ExportAsync(profile.Id, default);
            Assert.Equal(stored.FeedbackGivenAt, exported.Recommendations!.Single(r => r.Id == old.Id).FeedbackGivenAt);
        }
        Assert.Empty((await Read(other.Id)).FeedbackHistory!);
        await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryBatchStore>().SaveFeedbackAsync(profile.Id,
            old.Id, revision, UserFeedback.None, null, null, now.AddSeconds(1), ct));
        var cleared = await Read(profile.Id);
        await using (var scope = services.CreateAsyncScope())
            Assert.Equal(DiscoveryJobState.Cancelled, (await scope.ServiceProvider.GetRequiredService<DataContext>().DiscoveryJobs.SingleAsync(j => j.Id == pending.Id)).State);
        Assert.DoesNotContain("established rock", EffectiveTasteInputs.FromProfile(profile.Id, cleared.Signals,
            cleared.Feedback, new(), cleared.FeedbackHistory).FamiliarArtists);
    }

    [LocalPostgresMigrationFact]
    public async Task ResidueFeedbackHistoryDecodesOnlyWindowReceiptsAtOneThousandVotes()
    {
        var profile = await GuestAsync('a');
        var now = DateTimeOffset.UtcNow;
        await WriteAsync(async (s, ct) =>
        {
            var db = s.GetRequiredService<DataContext>();
            var batch = new DiscoveryBatch(profile.Id, "scalar-history", DiscoveryBatchKind.Initial, 0, now);
            batch.Complete(now); db.DiscoveryBatches.Add(batch);
            for (var i = 0; i < 1000; i++)
            {
                var pick = WeeklyRecommendation.ForDiscovery(profile.Id, batch.Id, null,
                    new Track("Recording " + i, "Artist " + i % 20), i + 1, new ScoreBreakdown());
                pick.RecordFeedback(i % 4 == 0 ? UserFeedback.Disliked : UserFeedback.Liked, now: now.UtcDateTime.AddSeconds(i - 1001));
                db.WeeklyRecommendations.Add(pick);
            }
            await db.SaveChangesAsync(ct); return true;
        }, 8_388_608);
        await using var scope = services.CreateAsyncScope();
        var watch = Stopwatch.StartNew();
        var inputs = (await scope.ServiceProvider.GetRequiredService<IDiscoveryBatchStore>().ReadInputsAsync(profile.Id, now, 20, default))!;
        Assert.Equal(1000, inputs.FeedbackHistory!.Count);
        Assert.Equal(20, inputs.Feedback.Count);
        var effective = EffectiveTasteInputs.FromProfile(profile.Id, inputs.Signals, inputs.Feedback, new(), inputs.FeedbackHistory);
        Assert.Equal(20, effective.FeedbackSupport.Count);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "Loopback 1000-vote read exceeds regression guard");
    }
    [LocalPostgresMigrationFact]
    public async Task ResidueFeedbackConcurrentEditsAndRetainedKnownMarkersRemainReversible()
    {
        var profile = await GuestAsync('a');
        var now = DateTimeOffset.UtcNow;
        var id = await WriteAsync(async (s, ct) =>
        {
            var db = s.GetRequiredService<DataContext>();
            var batch = new DiscoveryBatch(profile.Id, "feedback-race", DiscoveryBatchKind.Initial, 0, now);
            batch.Complete(now); db.DiscoveryBatches.Add(batch);
            var pick = WeeklyRecommendation.ForDiscovery(profile.Id, batch.Id, null,
                new Track("Distinct recording", "Artist"), 1, new ScoreBreakdown());
            db.WeeklyRecommendations.Add(pick); await db.SaveChangesAsync(ct); return pick.Id;
        });
        Task<long> Save(long revision, UserFeedback feedback, int? rating = null) => WriteAsync((s, ct) =>
            s.GetRequiredService<IDiscoveryBatchStore>().SaveFeedbackAsync(profile.Id, id, revision, feedback, rating, null, now, ct));
        async Task<bool> Compete(UserFeedback feedback)
        {
            try { await Save(0, feedback); return true; }
            catch (DiscoveryRequestConflictException) { return false; }
        }
        var outcomes = await Task.WhenAll(Compete(UserFeedback.Liked), Compete(UserFeedback.Disliked));
        Assert.Single(outcomes.Where(s => s));
        var revision = await Save(1, UserFeedback.AlreadyKnown);
        revision = await Save(revision, UserFeedback.Liked);
        async Task<EffectiveTasteInputs> Read()
        {
            await using var scope = services.CreateAsyncScope();
            var inputs = (await scope.ServiceProvider.GetRequiredService<IDiscoveryBatchStore>().ReadInputsAsync(profile.Id, now, 20, default))!;
            return EffectiveTasteInputs.FromProfile(profile.Id, inputs.Signals, inputs.Feedback, new(), inputs.FeedbackHistory);
        }
        var known = await Read();
        Assert.Contains("artist:distinct recording", known.ExcludedAliases);
        Assert.Equal(0, Assert.Single(known.FeedbackSupport).NegativeRecordings);
        await Save(revision, UserFeedback.None);
        var cleared = await Read();
        Assert.Empty(cleared.ExcludedAliases);
        Assert.Empty(cleared.PositiveSignals);
    }

}
