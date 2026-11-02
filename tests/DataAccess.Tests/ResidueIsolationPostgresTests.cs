using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Catalog;
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
    public async Task ResidueFeedbackOwnershipAndGuestDeletionDoNotChangeAnotherListenersTasteOrReceipt()
    {
        var a = await GuestAsync('a');
        var b = await GuestAsync('b');
        foreach (var profile in new[] { a, b })
        {
            await using var taste = services.CreateAsyncScope();
            await taste.ServiceProvider.GetRequiredService<SelectDiscoveryTaste>()
                .ReplaceAsync(profile.Id, 0, new([], [profile.Id == a.Id ? "rock" : "jazz"]), default);
        }
        var sharedRecording = Guid.NewGuid().ToString("D");
        async Task<Guid> Complete(Guid profile)
        {
            await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile,
                "isolation", new string('a', 64), DiscoveryJobKind.Generation, 1, "{}", DateTimeOffset.UtcNow, 10, TimeSpan.Zero, ct));
            var job = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(
                DiscoveryJobKind.Generation, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), 3, ct));
            var batch = Guid.NewGuid();
            var candidate = new BaselineACandidate(Track.Create("Shared public recording", "Shared public artist", mbid: sharedRecording),
                WeightedTagVector.Empty, ScoreEvidence.Empty);
            var score = new BaselineAScorer().ScoreForBatch(candidate, WeightedTagVector.Empty, false, batch, new());
            await WriteAsync(async (s, ct) =>
            {
                await s.GetRequiredService<IDiscoveryBatchStore>().CompleteAsync(job!.Id, job.LeaseToken!.Value,
                    batch, DiscoveryBatchKind.Initial, [new(candidate.Track, score)], "{}",
                    DateTimeOffset.UtcNow.AddMinutes(10), DateTimeOffset.UtcNow, ct);
                return true;
            });
            await using var scope = services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<DataContext>().WeeklyRecommendations
                .Where(p => p.ProfileId == profile).Select(p => p.Id).SingleAsync();
        }
        var aPick = await Complete(a.Id);
        var bPick = await Complete(b.Id);
        async Task<DiscoveryInputs> Read(Guid profile)
        {
            await using var scope = services.CreateAsyncScope();
            return (await scope.ServiceProvider.GetRequiredService<IDiscoveryBatchStore>()
                .ReadInputsAsync(profile, DateTimeOffset.UtcNow, 20, default))!;
        }
        var before = await Read(b.Id);
        await Assert.ThrowsAsync<LinerNotes.Application.Common.Exceptions.NotFoundException>(() =>
            WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryBatchStore>().SaveFeedbackAsync(a.Id, bPick, 1,
                UserFeedback.Disliked, 2, null, DateTimeOffset.UtcNow, ct)));
        await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryBatchStore>().SaveFeedbackAsync(a.Id, aPick, 1,
            UserFeedback.Disliked, 2, null, DateTimeOffset.UtcNow, ct));
        var after = await Read(b.Id);
        Assert.Equal(before.Profile.Revision, after.Profile.Revision);
        Assert.Equal(JsonSerializer.Serialize(before.Signals), JsonSerializer.Serialize(after.Signals));
        Assert.Empty(after.Feedback);
        var effective = EffectiveTasteInputs.FromProfile(b.Id, after.Signals, (await Read(a.Id)).Feedback, new());
        Assert.Empty(effective.ExcludedAliases);
        Assert.DoesNotContain(effective.PositiveSignals, s => s.NormalizedTargetValue == "rock");
        await using (var scope = services.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<IDiscoveryReader>();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var batch = await db.WeeklyRecommendations.Where(p => p.Id == bPick).Select(p => p.BatchId).SingleAsync();
            Assert.Null(await reader.ReadBatchAsync(a.Id, batch, DateTimeOffset.UtcNow, default));
            Assert.NotNull(await reader.ReadBatchAsync(b.Id, batch, DateTimeOffset.UtcNow, default));
            var stored = await db.WeeklyRecommendations.SingleAsync(p => p.Id == bPick);
            Assert.Equal(UserFeedback.None, stored.Feedback);
            Assert.Null(stored.Rating);
        }
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<GuestLifecycle>().ClearAsync(new string('a', 64), default);
        var surviving = await Read(b.Id);
        Assert.Equal(JsonSerializer.Serialize(before.Signals), JsonSerializer.Serialize(surviving.Signals));
        Assert.Equal(before.Profile.Revision, surviving.Profile.Revision);
    }
    [LocalPostgresMigrationFact]
    public async Task BaselineRecentNoteOnlyRowsCanDisplaceDurablePositiveFeedback()
    {
        var profile = await GuestAsync('a');
        await WriteAsync(async (services, ct) =>
        {
            var db = services.GetRequiredService<DataContext>();
            var now = DateTimeOffset.UtcNow;
            var batchId = Guid.NewGuid();
            var batch = new DiscoveryBatch(profile.Id, "window-baseline", DiscoveryBatchKind.Initial, 0, now, batchId);
            batch.Complete(now);
            db.DiscoveryBatches.Add(batch);
            for (int i = 0; i < 21; i++)
            {
                var pick = LinerNotes.Domain.Digest.WeeklyRecommendation.ForDiscovery(profile.Id, batchId, null,
                    Track.Create("Fixture recording " + i, "Fixture artist " + i), i + 1, new ScoreBreakdown());
                pick.RecordFeedback(i == 0 ? UserFeedback.Liked : UserFeedback.None, i == 0 ? null : "Uninterpreted note");
                db.WeeklyRecommendations.Add(pick);
                db.Entry(pick).Property(p => p.FeedbackGivenAt).CurrentValue = now.UtcDateTime.AddMinutes(i - 21);
            }
            await db.SaveChangesAsync(ct);
            return true;
        });
        await using var scope = services.CreateAsyncScope();
        var inputs = (await scope.ServiceProvider.GetRequiredService<IDiscoveryBatchStore>()
            .ReadInputsAsync(profile.Id, DateTimeOffset.UtcNow, 20, default))!;
        Assert.Equal(20, inputs.Feedback.Count);
        Assert.All(inputs.Feedback, p => Assert.Equal(UserFeedback.None, p.Feedback));
        Assert.Empty(EffectiveTasteInputs.FromProfile(profile.Id, inputs.Signals, inputs.Feedback, new()).PositiveSignals);
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<DataContext>().WeeklyRecommendations
            .CountAsync(p => p.ProfileId == profile.Id && p.Feedback == UserFeedback.Liked));
        // Characterization of the pre-renewal defect, not the desired future behavior.
    }

}
