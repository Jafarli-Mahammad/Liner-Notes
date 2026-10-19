using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class DiscoveryCoordinationPostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task CompletingBatchKeepsRecordingIdentityAcrossListenersWithIdenticalNames()
    {
        var original = Track.Create("Same title", "Same artist", mbid: Guid.NewGuid().ToString("D"));
        await WriteAsync(async (s, ct) =>
        {
            var db = s.GetRequiredService<DataContext>();
            db.Tracks.Add(original);
            await db.SaveChangesAsync(ct);
            return true;
        });
        var recording = Guid.NewGuid().ToString("D");
        Guid? firstRecordingId = null;
        var mbids = new string?[] { recording, null, recording.ToUpperInvariant() };
        for (int i = 0; i < mbids.Length; i++)
        {
            var profile = await GuestAsync((char)('a' + i));
            var now = DateTimeOffset.UtcNow;
            await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id,
                "recording-identity", new string('a', 64), DiscoveryJobKind.Generation, 0, "{}", now, 10, TimeSpan.Zero, ct));
            var job = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(
                DiscoveryJobKind.Generation, now, TimeSpan.FromMinutes(2), 3, ct));
            var batchId = Guid.NewGuid();
            var candidate = Track.Create("Same title", "Same artist", mbid: mbids[i]);
            var score = new BaselineAScorer().ScoreForBatch(new(candidate, WeightedTagVector.Empty, ScoreEvidence.Empty),
                WeightedTagVector.Empty, false, batchId, new());
            await WriteAsync(async (s, ct) =>
            {
                await s.GetRequiredService<IDiscoveryBatchStore>().CompleteAsync(job!.Id, job.LeaseToken!.Value,
                    batchId, DiscoveryBatchKind.Initial, [new(candidate, score)], "{}", null, now, ct);
                return true;
            });
            await using var scope = services.CreateAsyncScope();
            var saved = await scope.ServiceProvider.GetRequiredService<DataContext>().WeeklyRecommendations
                .Include(r => r.Track).SingleAsync(r => r.ProfileId == profile.Id);
            Assert.Equal(candidate.TrackKey, saved.Track.TrackKey);
            Assert.NotEqual(original.Id, saved.Track.Id);
            if (i == 0) firstRecordingId = saved.Track.Id;
            if (i == 2) Assert.Equal(firstRecordingId, saved.Track.Id);
        }
    }
}

public sealed partial class Phase6PostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task WeeklySaveRejectsChangedAnswersWithNoSignalsOrFeedback()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var profile = await db.ListenerProfiles.AsTracking().SingleAsync(p => p.UserId == userId);
        db.TasteSignals.RemoveRange(await db.TasteSignals.ToArrayAsync());
        profile.SetOnboarding(JsonSerializer.Serialize(new TasteOnboarding(AllStyles: true)));
        await db.SaveChangesAsync();
        var store = scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>();
        await using var batch = await store.AcquireBatchAsync();
        var input = await store.ReadInputsAsync(userId);
        Assert.Empty(input.Signals);
        Assert.Empty(input.Feedback);

        profile.SetOnboarding(JsonSerializer.Serialize(new TasteOnboarding(Goal: "different-styles", AllStyles: true)));
        await db.SaveChangesAsync();
        var changedAnswers = await store.ReadInputsAsync(userId);
        Assert.NotEqual(input.Revision, changedAnswers.Revision);
        profile.ChangeInputs(profile.Revision);
        await db.SaveChangesAsync();
        Assert.NotEqual(changedAnswers.Revision, (await store.ReadInputsAsync(userId)).Revision);
        var error = await Assert.ThrowsAsync<GenerationStoppedException>(() => store.PersistAsync(
            userId, Week, input.Revision, [], new FailedReservation()));
        Assert.Equal("taste_revision_changed", error.Message);
        Assert.Empty(await db.WeeklyDigests.IgnoreQueryFilters().ToArrayAsync());
        Assert.Empty(await db.WeeklyRecommendations.IgnoreQueryFilters().ToArrayAsync());
    }
}
