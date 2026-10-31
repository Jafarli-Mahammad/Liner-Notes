using LinerNotes.Application;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.Domain.Discovery;
using LinerNotes.Infrastructure;
using LinerNotes.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
namespace LinerNotes.DataAccess.Tests;

public sealed partial class DiscoveryCoordinationPostgresTests : IAsyncLifetime
{
    [LocalPostgresMigrationFact]
    public async Task WeeklyScheduleDigestAndUnsubscribeLeaveDiscoveryInventoryUsable()
    {
        await services.DisposeAsync();
        registrations.AddSingleton(new DiscoveryRuntimeOptions { RunJobs = true });
        services = registrations.BuildServiceProvider();
        // Enabling the durable protocol requires the same explicit establishment as deployment.
        await ReconcileAsync();
        var userId = await WriteAsync(async (s, ct) =>
        {
            var db = s.GetRequiredService<DataContext>();
            var user = new LinerNotes.Domain.Digest.User("weekly-coordination@example.test", weeklyEmailOptIn: true);
            user.SetNextDigestAt(new DateTime(2026, 10, 18, 8, 0, 0, DateTimeKind.Utc));
            db.ApplicationUsers.Add(new LinerNotes.DataAccess.IdentityEntities.ApplicationUser
            {
                Id = user.Id, UserName = "weekly-coordination", NormalizedUserName = "WEEKLY-COORDINATION",
                Email = user.Email, NormalizedEmail = "WEEKLY-COORDINATION@EXAMPLE.TEST", EmailConfirmed = true
            });
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            return user.Id;
        });
        var now = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        await using (var scope = services.CreateAsyncScope())
        {
            var schedule = scope.ServiceProvider.GetRequiredService<LinerNotes.Application.Common.Interfaces.IWeeklyScheduleStore>();
            var selected = Assert.Single(await schedule.GetCandidatesAsync([userId], 10, now, default));
            var week = LinerNotes.Domain.Digest.IsoWeek.FromDate(now);
            var handler = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var result = await handler.Send(new LinerNotes.Application.Features.Digests.Commands.GenerateDigest.GenerateDigestCommand(userId, week));
            Assert.Equal("generated", result.Status);
            Assert.NotNull(result.Digest);
            Assert.True(await schedule.AdvanceAsync(selected, new DateTime(2026, 10, 25, 8, 0, 0, DateTimeKind.Utc), now, default));
            await scope.ServiceProvider.GetRequiredService<LinerNotes.Application.Common.Interfaces.IUnsubscribeStore>()
                .UnsubscribeAsync(userId, now, default);
            Assert.Empty(await schedule.GetCandidatesAsync([userId], 10, now.AddDays(8), default));
        }
        Assert.NotNull(await GuestAsync('e'));
        await using var verification = services.CreateAsyncScope();
        await verification.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        var dbCheck = verification.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Equal(1, await dbCheck.WeeklyDigests.CountAsync());
        Assert.Equal(1, await dbCheck.DiscoveryBatches.CountAsync());
    }

    [LocalPostgresMigrationFact]
    public async Task EmptySupportedBatchCompletesWithoutWritingRecommendations()
    {
        var profile = await GuestAsync('a');
        await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id,
            "empty-batch", new string('a', 64), DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow, 10, TimeSpan.Zero, ct));
        var job = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(
            DiscoveryJobKind.Generation, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(60), 3, ct));
        var batch = Guid.NewGuid();
        await WriteAsync(async (s, ct) =>
        {
            await s.GetRequiredService<IDiscoveryBatchStore>().CompleteAsync(job!.Id, job.LeaseToken!.Value, batch,
                DiscoveryBatchKind.OnDemand, [], "{\"SupportedPicks\":0}", DateTimeOffset.UtcNow.AddMinutes(1), DateTimeOffset.UtcNow, ct);
            return true;
        });
        await using var scope = services.CreateAsyncScope();
        var view = await scope.ServiceProvider.GetRequiredService<IDiscoveryReader>().ReadBatchAsync(profile.Id, batch, DateTimeOffset.UtcNow, default);
        Assert.NotNull(view); Assert.Empty(view.Picks);
        Assert.Equal(DiscoveryJobState.Completed, (await scope.ServiceProvider.GetRequiredService<DataContext>().DiscoveryJobs.SingleAsync()).State);
        Assert.False(await scope.ServiceProvider.GetRequiredService<DataContext>().WeeklyRecommendations.AnyAsync());
    }
    [LocalPostgresMigrationFact]
    public async Task ReclamationRejectsGrowthAndAllowsActualGuestDeletion()
    {
        var profile = await GuestAsync('a');
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var writes = scope.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>();
            var failure = await Assert.ThrowsAsync<GenerationStoppedException>(() => writes.ReclaimAsync(async ct =>
            {
                db.ListenerProfiles.Add(ListenerProfile.Guest(DateTimeOffset.UtcNow, TimeSpan.FromDays(7)));
                await db.SaveChangesAsync(ct);
                return true;
            }, default));
            Assert.Equal("reclamation_increased_storage", failure.Message);
        }
        await using (var scope = services.CreateAsyncScope())
        {
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<DataContext>().ListenerProfiles.CountAsync());
            await scope.ServiceProvider.GetRequiredService<GuestLifecycle>().ClearAsync(new string('a', 64), default);
        }
        await using (var scope = services.CreateAsyncScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<DataContext>().ListenerProfiles.AnyAsync(p => p.Id == profile.Id));
        await GuestAsync('b'); // Committed reclamation published the next usable inventory revision.
    }
    [LocalPostgresMigrationFact]
    public async Task LegacyInputChangesAdvanceRevisionAndCancelOnlyGenerationAtomically()
    {
        var profile = await WriteAsync(async (s, ct) =>
        {
            var db = s.GetRequiredService<DataContext>();
            var user = new LinerNotes.Domain.Digest.User("input-version@example.test", "UTC");
            var owner = user.Profile!;
            db.ApplicationUsers.Add(new LinerNotes.DataAccess.IdentityEntities.ApplicationUser
            {
                Id = user.Id, UserName = "input-version", NormalizedUserName = "INPUT-VERSION",
                Email = user.Email, NormalizedEmail = "INPUT-VERSION@EXAMPLE.TEST"
            });
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            return owner;
        });
        foreach (var kind in new[] { DiscoveryJobKind.Generation, DiscoveryJobKind.Lookup })
            await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id,
                kind.ToString(), new string('a', 64), kind, 0, "{}", DateTimeOffset.UtcNow, 10, TimeSpan.Zero, ct));

        async Task<bool> Change(IServiceProvider s, CancellationToken ct)
        {
            await s.GetRequiredService<LinerNotes.Application.Common.Interfaces.Repositories.IListenerProfileRepository>()
                .ChangeInputsAsync(profile.UserId!.Value, ct);
            await s.GetRequiredService<DataContext>().SaveChangesAsync(ct);
            return true;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync<bool>(async (s, ct) =>
        {
            await Change(s, ct);
            throw new InvalidOperationException("synthetic rollback");
        }));
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            Assert.Equal(0, (await db.ListenerProfiles.SingleAsync(p => p.Id == profile.Id)).Revision);
            Assert.All(await db.DiscoveryJobs.ToArrayAsync(), j => Assert.Equal(DiscoveryJobState.Pending, j.State));
        }
        await WriteAsync(Change);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            Assert.Equal(1, (await db.ListenerProfiles.SingleAsync(p => p.Id == profile.Id)).Revision);
            Assert.Equal(DiscoveryJobState.Cancelled, (await db.DiscoveryJobs.SingleAsync(j => j.Kind == DiscoveryJobKind.Generation)).State);
            Assert.Equal(DiscoveryJobState.Pending, (await db.DiscoveryJobs.SingleAsync(j => j.Kind == DiscoveryJobKind.Lookup)).State);
        }
    }
    [LocalPostgresMigrationFact]
    public async Task DiscoveryReadsAndFeedbackAreOwnedAndFeedbackCancelsStaleWorkWithoutRewritingScores()
    {
        var profile = await GuestAsync('a'); var other = await GuestAsync('b');
        await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id, "first", new string('a', 64),
            DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow, 10, TimeSpan.Zero, ct));
        var leased = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(DiscoveryJobKind.Generation,
            DateTimeOffset.UtcNow, TimeSpan.FromSeconds(60), 3, ct));
        Guid batchId = Guid.NewGuid();
        var track = new LinerNotes.Domain.Catalog.Track("Song", "Artist");
        var vector = LinerNotes.Domain.Catalog.WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["jazz"] = 1 });
        var breakdown = new LinerNotes.Domain.Scoring.BaselineAScorer().ScoreForBatch(
            new(track, vector, LinerNotes.Domain.Scoring.ScoreEvidence.Empty), vector, false, batchId, new());
        await WriteAsync(async (s, ct) => { await s.GetRequiredService<IDiscoveryBatchStore>().CompleteAsync(leased!.Id,
            leased.LeaseToken!.Value, batchId, DiscoveryBatchKind.Initial, [new(track, breakdown)], "{}", null, DateTimeOffset.UtcNow, ct); return true; }, 1_048_576);
        await using var scope = services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IDiscoveryReader>();
        Assert.Null(await reader.ReadBatchAsync(other.Id, batchId, DateTimeOffset.UtcNow, default));
        var batch = await reader.ReadBatchAsync(profile.Id, batchId, DateTimeOffset.UtcNow, default);
        var pick = Assert.Single(batch!.Picks);
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Task<string> Hash() => db.Database.SqlQuery<string>($"SELECT md5(\"ScoreBreakdown\"::text) AS \"Value\" FROM \"WeeklyRecommendations\" WHERE \"Id\"={pick.Id}").SingleAsync();
        string before = await Hash();
        var pending = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id, "next", new string('b', 64),
            DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow, 10, TimeSpan.Zero, ct));
        var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
        await Assert.ThrowsAsync<LinerNotes.Application.Common.Exceptions.NotFoundException>(() => sender.Send(new RecordDiscoveryFeedback(other.Id,
            pick.Id, 0, LinerNotes.Domain.Enums.UserFeedback.Liked)));
        var feedback = new RecordDiscoveryFeedback(profile.Id, pick.Id, 0, LinerNotes.Domain.Enums.UserFeedback.Liked, 8, "Fixture comment.");
        Assert.Equal(1, await sender.Send(feedback));
        Assert.Equal(1, await sender.Send(feedback));
        Assert.Equal(before, await Hash());
        Assert.Equal(DiscoveryJobState.Cancelled, (await db.DiscoveryJobs.AsNoTracking().SingleAsync(j => j.Id == pending.Id)).State);
        var page = await reader.ReadAsync(profile.Id, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30), default);
        Assert.Equal(1, page!.Revision);
        var after = Assert.Single((await reader.ReadBatchAsync(profile.Id, batchId, DateTimeOffset.UtcNow, default))!.Picks);
        Assert.Equal(8, after.Rating); Assert.Equal("Fixture comment.", after.Comment);
        Assert.Equal(2, await sender.Send(new RecordDiscoveryFeedback(profile.Id, pick.Id, 1, LinerNotes.Domain.Enums.UserFeedback.None)));
        await Assert.ThrowsAsync<DiscoveryRequestConflictException>(() => sender.Send(feedback));
        Assert.Equal(before, await Hash());
    }
    private sealed class CountingLookup : LinerNotes.Application.Common.Interfaces.Catalog.ICatalogLookupSource
    {
        public int Calls;
        public bool Retainable = true;
        private readonly LinerNotes.Infrastructure.Catalog.MusicBrainzCatalogLookupSource source = new(
            new LinerNotes.Infrastructure.RecommendationSources.ListenBrainz.FixtureListenBrainzApiClient(), TimeProvider.System);
        public Task<LinerNotes.Application.Common.Interfaces.Catalog.CatalogLookupResult> SearchAsync(string query,
            LinerNotes.Domain.Catalog.CatalogKind kind, CancellationToken ct)
        { return Search(); async Task<LinerNotes.Application.Common.Interfaces.Catalog.CatalogLookupResult> Search() {
            Calls++; var result = await source.SearchAsync(query, kind, ct);
            return Retainable ? result : result with { CacheUntilUtc = result.AcquiredAtUtc };
        } }
    }

    [LocalPostgresMigrationFact]
    public async Task LookupWithoutRetentionPermissionCannotPublishSelectableCatalogData()
    {
        await services.DisposeAsync();
        registrations.AddSingleton(new DiscoveryRuntimeOptions { RunJobs = true });
        registrations.AddSingleton<LinerNotes.Application.Common.Interfaces.Catalog.ICatalogLookupSource>(new CountingLookup { Retainable = false });
        services = registrations.BuildServiceProvider(); await ReconcileAsync();
        var profile = await GuestAsync('a');
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MediatR.ISender>().Send(new LinerNotes.Application.Features.Catalog.SubmitCatalogLookup(
            profile.Id, "Synthetic album", LinerNotes.Domain.Catalog.CatalogKind.Album, "lookup"));
        var lease = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(DiscoveryJobKind.Lookup,
            DateTimeOffset.UtcNow, TimeSpan.FromSeconds(60), 3, ct, DiscoveryJobLane.Acquisition));
        var error = await Assert.ThrowsAsync<GenerationStoppedException>(() => scope.ServiceProvider
            .GetRequiredService<LinerNotes.Application.Features.Catalog.RunCatalogLookup>().ExecuteAsync(lease!, default));
        Assert.Equal("catalog_lookup_not_retainable", error.Message);
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Equal(0, await db.CatalogPublications.CountAsync());
        Assert.Equal(0, await db.Set<LinerNotes.DataAccess.Persistence.Accounting.PublicEvidenceCacheEntry>().CountAsync());
    }

    [LocalPostgresMigrationFact]
    public async Task ExplicitAlbumLookupPublishesLocallyAndReusesPublicAcquisitionAcrossProfiles()
    {
        await services.DisposeAsync();
        var source = new CountingLookup();
        registrations.AddSingleton(new DiscoveryRuntimeOptions { RunJobs = true });
        registrations.AddSingleton<LinerNotes.Application.Common.Interfaces.Catalog.ICatalogLookupSource>(source);
        services = registrations.BuildServiceProvider(); await ReconcileAsync();
        var first = await GuestAsync('a'); var second = await GuestAsync('b');
        await using (var tasteScope = services.CreateAsyncScope())
            await tasteScope.ServiceProvider.GetRequiredService<SelectDiscoveryTaste>().ReplaceAsync(first.Id, 0, new([], ["jazz"]), default);
        Guid? publication = null;
        foreach (var profile in new[] { first, second })
        {
            await using var scope = services.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
            var accepted = await sender.Send(new LinerNotes.Application.Features.Catalog.SubmitCatalogLookup(profile.Id,
                profile.Id == first.Id ? "Synthetic album" : "SYNTHETIC ALBUM", LinerNotes.Domain.Catalog.CatalogKind.Album, "lookup"));
            Assert.Equal("Pending", accepted.State);
            var lease = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(DiscoveryJobKind.Lookup,
                DateTimeOffset.UtcNow, TimeSpan.FromSeconds(60), 3, ct, DiscoveryJobLane.Acquisition));
            Assert.NotNull(lease);
            await scope.ServiceProvider.GetRequiredService<LinerNotes.Application.Features.Catalog.RunCatalogLookup>().ExecuteAsync(lease, default);
            var result = await sender.Send(new LinerNotes.Application.Features.Catalog.GetCatalogLookup(profile.Id, lease.Id));
            Assert.Equal("Completed", result!.State);
            var item = Assert.Single(result.Items!);
            Assert.Equal(LinerNotes.Domain.Catalog.CatalogKind.Album, item.Kind);
            Assert.NotEmpty(item.Artists!);
            publication ??= item.Version; Assert.Equal(publication, item.Version);
            Assert.Equal(accepted.Id, (await sender.Send(new LinerNotes.Application.Features.Catalog.SubmitCatalogLookup(profile.Id,
                "synthetic album", LinerNotes.Domain.Catalog.CatalogKind.Album, "lookup"))).Id);
            Assert.Null(await sender.Send(new LinerNotes.Application.Features.Catalog.GetCatalogLookup(
                profile.Id == first.Id ? second.Id : first.Id, lease.Id)));
        }
        Assert.Equal(1, source.Calls);
        await using var verify = services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Equal(1, await db.CatalogPublications.CountAsync());
        Assert.Null((await db.CatalogHeads.SingleAsync()).PublicationId);
        Assert.All(await db.DiscoveryJobs.ToArrayAsync(), j => { Assert.Equal("{}", j.PayloadJson); Assert.DoesNotContain("Synthetic album", j.ResultJson!); });
        var catalog = verify.ServiceProvider.GetRequiredService<LinerNotes.Application.Common.Interfaces.Catalog.ICatalogReader>();
        Assert.NotEmpty((await catalog.SearchAsync("synthetic", LinerNotes.Domain.Catalog.CatalogKind.Album, 10, 0, DateTimeOffset.UtcNow, default)).Items);
        Assert.Equal(1, source.Calls);
    }
    private sealed class SubmissionClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [LocalPostgresMigrationFact]
    public async Task SubmissionReplaysBeforeChangingTasteAndCooldownRollsBackCancellationAndInputs()
    {
        await services.DisposeAsync();
        var clock = new SubmissionClock();
        registrations.AddSingleton<TimeProvider>(clock);
        registrations.AddSingleton(new DiscoveryRuntimeOptions { RunJobs = true });
        services = registrations.BuildServiceProvider();
        await ReconcileAsync();
        var profile = await GuestAsync('a');
        async Task<DiscoveryJob> Submit(string key, long revision, DiscoveryTasteSelection? selection)
        {
            await using var scope = services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<MediatR.IMediator>().Send(new SubmitDiscovery(profile.Id, revision, key, selection));
        }
        var initial = await Submit("initial", 0, new([], ["jazz"]));
        Assert.Equal(initial.Id, (await Submit("initial", 0, new([], ["JAZZ"]))).Id);
        await Assert.ThrowsAsync<DiscoveryRequestConflictException>(() => Submit("initial", 0, new([], ["ambient"])));
        await Assert.ThrowsAsync<DiscoveryBusyException>(() => Submit("changed", 1, new([], ["ambient"])));
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            Assert.Equal(1, (await db.ListenerProfiles.SingleAsync()).Revision);
            Assert.Equal("jazz", (await db.TasteSignals.SingleAsync()).NormalizedTargetValue);
            Assert.Equal(DiscoveryJobState.Pending, (await db.DiscoveryJobs.SingleAsync()).State);
        }
        clock.Now += TimeSpan.FromSeconds(31);
        var changed = await Submit("changed", 1, new([], ["ambient"]));
        Assert.Equal(2, changed.InputRevision);
        await Assert.ThrowsAsync<DiscoveryRequestConflictException>(() => Submit("stale", 1, null));
        await using var verify = services.CreateAsyncScope();
        var final = verify.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Equal("ambient", (await final.TasteSignals.SingleAsync()).NormalizedTargetValue);
        Assert.Equal(DiscoveryJobState.Cancelled, (await final.DiscoveryJobs.SingleAsync(j => j.Id == initial.Id)).State);
        Assert.Equal(2, await final.DiscoveryJobs.CountAsync());
    }
    [LocalPostgresMigrationFact]
    public async Task PreparedLaneSkipsAcquisitionWaitAndHandoffDoesNotConsumeAttempt()
    {
        var first = await GuestAsync('a'); var second = await GuestAsync('b');
        foreach (var profile in new[] { first, second })
            await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id, "initial", new string('a', 64),
                DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow, 10, TimeSpan.Zero, ct));
        var leased = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(DiscoveryJobKind.Generation,
            DateTimeOffset.UtcNow, TimeSpan.FromSeconds(60), 1, ct, DiscoveryJobLane.Prepared));
        Assert.Equal(first.Id, leased!.ProfileId);
        await WriteAsync(async (s, ct) => { await s.GetRequiredService<IDiscoveryJobStore>().DeferAsync(leased.Id,
            leased.LeaseToken!.Value, DateTimeOffset.UtcNow, TimeSpan.Zero, "waiting_for_music", ct); return true; });
        var prepared = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(DiscoveryJobKind.Generation,
            DateTimeOffset.UtcNow, TimeSpan.FromSeconds(60), 1, ct, DiscoveryJobLane.Prepared));
        Assert.Equal(second.Id, prepared!.ProfileId);
        var acquisition = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(DiscoveryJobKind.Generation,
            DateTimeOffset.UtcNow, TimeSpan.FromSeconds(60), 1, ct, DiscoveryJobLane.Acquisition));
        Assert.Equal(first.Id, acquisition!.ProfileId);
        Assert.Equal(1, acquisition.Attempts);
        await using var scope = services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<IDiscoveryJobStore>().HasReadyAsync(DiscoveryJobKind.Generation,
            DiscoveryJobLane.Prepared, DateTimeOffset.UtcNow.AddMinutes(2), 1, default));
        Assert.Null(await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(DiscoveryJobKind.Generation,
            DateTimeOffset.UtcNow.AddMinutes(2), TimeSpan.FromSeconds(60), 1, ct, DiscoveryJobLane.Prepared)));
        Assert.All(await scope.ServiceProvider.GetRequiredService<DataContext>().DiscoveryJobs.ToArrayAsync(), j => Assert.Equal(DiscoveryJobState.Cancelled, j.State));
    }
    [LocalPostgresMigrationFact]
    public async Task BatchCompletionAtomicallyStoresScoresHistoryAndJobThenHistorySurvivesSnapshotRemoval()
    {
        var profile = await GuestAsync('a');
        await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id, "initial", new string('a', 64),
            DiscoveryJobKind.Generation, profile.Revision, "{}", DateTimeOffset.UtcNow, 10, TimeSpan.Zero, ct));
        var lease = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(
            DiscoveryJobKind.Generation, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(60), 3, ct));
        Guid batchId = Guid.NewGuid();
        var track = new LinerNotes.Domain.Catalog.Track("Song", "Artist", mbid: Guid.NewGuid().ToString("D"));
        var vector = LinerNotes.Domain.Catalog.WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["jazz"] = 1 });
        var scorer = new LinerNotes.Domain.Scoring.BaselineAScorer();
        var breakdown = scorer.ScoreForBatch(new(track, vector, LinerNotes.Domain.Scoring.ScoreEvidence.Empty), vector, false, batchId, new());
        await WriteAsync(async (s, ct) => {
            await s.GetRequiredService<IDiscoveryBatchStore>().CompleteAsync(lease!.Id, lease.LeaseToken!.Value, batchId,
                DiscoveryBatchKind.Initial, [new(track, breakdown)], "{}", DateTimeOffset.UtcNow.AddMinutes(1), DateTimeOffset.UtcNow, ct);
            return true;
        }, 1_048_576);
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            Assert.Equal(DiscoveryJobState.Completed, (await db.DiscoveryJobs.SingleAsync()).State);
            Assert.Equal(batchId, (await db.WeeklyRecommendations.SingleAsync()).ScoreBreakdown.Snapshot!.DiscoveryBatchId);
            Assert.Equal(2, await db.Set<LinerNotes.DataAccess.Persistence.Accounting.RecommendedIdentity>().CountAsync());
        }
        await WriteAsync(async (s, ct) => { await s.GetRequiredService<DataContext>().WeeklyRecommendations.ExecuteDeleteAsync(ct); return true; });
        await using (var scope = services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IDiscoveryBatchStore>();
            Assert.Equal(2, (await store.FindExcludedAsync(profile.Id, LinerNotes.Domain.Scoring.TrackIdentity.Aliases(track).ToArray(), default)).Count);
        }
        await WriteAsync(async (s, ct) => { await s.GetRequiredService<DataContext>().ListenerProfiles.ExecuteDeleteAsync(ct); return true; });
        await using var verify = services.CreateAsyncScope();
        Assert.Equal(0, await verify.ServiceProvider.GetRequiredService<DataContext>().Set<LinerNotes.DataAccess.Persistence.Accounting.RecommendedIdentity>().CountAsync());
    }

    [LocalPostgresMigrationFact]
    public async Task StaleProfileRevisionAndExpiredEvidenceCannotPublishBatch()
    {
        var profile = await GuestAsync('b');
        await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id, "initial", new string('a', 64),
            DiscoveryJobKind.Generation, profile.Revision, "{}", DateTimeOffset.UtcNow, 10, TimeSpan.Zero, ct));
        var lease = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(
            DiscoveryJobKind.Generation, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(60), 3, ct));
        await Assert.ThrowsAsync<GenerationStoppedException>(() => WriteAsync(async (s, ct) => {
            await s.GetRequiredService<IDiscoveryBatchStore>().CompleteAsync(lease!.Id, lease.LeaseToken!.Value, Guid.NewGuid(),
                DiscoveryBatchKind.Initial, [], "{}", DateTimeOffset.UtcNow.AddSeconds(-1), DateTimeOffset.UtcNow, ct); return true;
        }));
        await WriteAsync(async (s, ct) => {
            var db = s.GetRequiredService<DataContext>();
            var current = await db.ListenerProfiles.AsTracking().SingleAsync(ct);
            current.ChangeInputs(current.Revision); await db.SaveChangesAsync(ct); return true;
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(async (s, ct) => {
            await s.GetRequiredService<IDiscoveryBatchStore>().CompleteAsync(lease!.Id, lease.LeaseToken!.Value, Guid.NewGuid(),
                DiscoveryBatchKind.Initial, [], "{}", null, DateTimeOffset.UtcNow, ct); return true;
        }));
        await using var scope = services.CreateAsyncScope();
        var verify = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Equal(0, await verify.DiscoveryBatches.CountAsync());
        Assert.Equal(0, await verify.WeeklyRecommendations.CountAsync());
        Assert.Equal(DiscoveryJobState.Running, (await verify.DiscoveryJobs.SingleAsync()).State);
    }

    private readonly string name = "liner_coord_c7_" + Guid.NewGuid().ToString("N");
    private readonly string root = Path.Combine(Path.GetTempPath(), "liner-coord-c7-" + Guid.NewGuid().ToString("N"));
    private string admin = "", connection = "";
    private ServiceProvider services = null!;
    private IServiceCollection registrations = null!;
    public async Task InitializeAsync()
    {
        admin = Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES") ?? "";
        if (admin.Length == 0) return;
        var settings = new NpgsqlConnectionStringBuilder(admin);
        Assert.Contains(settings.Host, new[] { "localhost", "127.0.0.1", "::1" });
        await using (var db = new NpgsqlConnection(admin))
        {
            await db.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", db); await create.ExecuteNonQueryAsync();
        }
        settings.Database = name; settings.Pooling = false; connection = settings.ConnectionString;
        Directory.CreateDirectory(root);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:DefaultConnection"] = connection, ["Generation:Origin"] = "Synthetic", ["LastFm:Mode"] = "FixtureOnly"
        }).Build();
        registrations = new ServiceCollection().AddLogging().AddApplication().AddDataAccess(config).AddInfrastructure(config);
        registrations.AddSingleton(new GenerationStorageOptions {
            InventoryPath = Path.Combine(root, "inventory.json"), LeasePath = Path.Combine(root, "lease"), DatabaseOverheadBytesPerPick = 1_048_576,
            ArtifactRoots = LocalGenerationStorage.Categories.ToDictionary(k => k, _ => Array.Empty<string>()) });
        registrations.AddSingleton(new CoordinatedStorageOptions { FilesystemPaths = [root], MinimumFreeBytes = 67_108_864 });
        // Crash checks must observe server rollback, not an automatic replay of the test fault.
        registrations.AddDbContext<DataContext>(b => b.UseNpgsql(connection, n => n.ExecutionStrategy(d => new NonRetryingExecutionStrategy(d))));
        services = registrations.BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        await ReconcileAsync();
    }
    public async Task DisposeAsync()
    {
        if (services is not null) await services.DisposeAsync();
        if (admin.Length > 0)
        {
            await using var db = new NpgsqlConnection(admin); await db.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", db); await drop.ExecuteNonQueryAsync();
        }
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
    private async Task ReconcileAsync()
    {
        await using var scope = services.CreateAsyncScope();
        await using var batch = await scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>().AcquireBatchAsync();
        await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        var state = await scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync();
        await scope.ServiceProvider.GetRequiredService<LocalGenerationStorage>().ReconcileAsync(state);
    }
    private async Task<ListenerProfile> GuestAsync(char hash)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GuestLifecycle>().StartAsync(new string(hash, 64), default);
    }
    private async Task<T> WriteAsync<T>(Func<IServiceProvider, CancellationToken, Task<T>> operation, long estimate = 65_536)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDiscoveryWriteCoordinator>().ExecuteAsync(estimate, ct => operation(scope.ServiceProvider, ct), default);
    }
    [LocalPostgresMigrationFact]
    public async Task EnqueueReplayConflictCooldownOwnershipAndLeaseFencingSurviveFreshScopes()
    {
        var profile = await GuestAsync('c');
        var other = await GuestAsync('d');
        var job = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id, "first", new string('a', 64),
            DiscoveryJobKind.Generation, 0, "{\"initial\":true}", DateTimeOffset.UtcNow, 32, TimeSpan.FromSeconds(30), ct));
        var replay = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id, "first", new string('a', 64),
            DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow, 32, TimeSpan.FromSeconds(30), ct));
        Assert.Equal(job.Id, replay.Id);
        await Assert.ThrowsAsync<DiscoveryRequestConflictException>(() => WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id,
            "first", new string('b', 64), DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow, 32, TimeSpan.Zero, ct)));
        var first = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(DiscoveryJobKind.Generation, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5), 3, ct));
        Assert.NotNull(first);
        var reclaimedAt = first!.LeaseExpiresAtUtc!.Value;
        var second = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(DiscoveryJobKind.Generation, reclaimedAt, TimeSpan.FromSeconds(5), 3, ct));
        Assert.NotEqual(first.LeaseToken, second!.LeaseToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(async (s, ct) => {
            await s.GetRequiredService<IDiscoveryJobStore>().RenewAsync(job.Id, first.LeaseToken!.Value, reclaimedAt.AddSeconds(1), TimeSpan.FromSeconds(5), "scoring", ct); return true; }));
        await WriteAsync(async (s, ct) => { await s.GetRequiredService<IDiscoveryJobStore>().FailAsync(job.Id, second.LeaseToken!.Value,
            reclaimedAt.AddSeconds(1), "unsupported_source", false, TimeSpan.Zero, 3, ct); return true; });
        await Assert.ThrowsAsync<DiscoveryBusyException>(() => WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id,
            "more", new string('a', 64), DiscoveryJobKind.Generation, 0, "{}", job.CreatedAtUtc.AddSeconds(2), 32, TimeSpan.FromSeconds(30), ct)));
        await using var read = services.CreateAsyncScope();
        var store = read.ServiceProvider.GetRequiredService<IDiscoveryJobStore>();
        Assert.Null(await store.FindAsync(other.Id, job.Id, DateTimeOffset.UtcNow, default));
        var saved = await store.FindAsync(profile.Id, job.Id, DateTimeOffset.UtcNow, default);
        Assert.Equal(DiscoveryJobState.Failed, saved!.State); Assert.Equal("{}", saved.PayloadJson);
    }
    [LocalPostgresMigrationFact]
    public async Task CompetingSubmissionsProduceOnePendingJobAndFailedInputJobActionRollsBack()
    {
        var profile = await GuestAsync('e');
        async Task<bool> Submit(string key)
        {
            try { await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id, key, new string('a', 64),
                DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow, 32, TimeSpan.Zero, ct)); return true; }
            catch (DiscoveryBusyException) { return false; }
            catch (GenerationStoppedException ex) when (ex.Message == "storage_lease_unavailable") { return false; }
        }
        var results = await Task.WhenAll(Submit("one"), Submit("two"));
        Assert.Single(results, result => result);
        var another = await GuestAsync('f');
        await Assert.ThrowsAsync<IOException>(() => WriteAsync(async (s, ct) => {
            await s.GetRequiredService<SelectDiscoveryTaste>().ReplaceAsync(another.Id, 0, new([], ["jazz"]), ct);
            await s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(another.Id, "atomic", new string('a', 64), DiscoveryJobKind.Generation,
                1, "{}", DateTimeOffset.UtcNow, 32, TimeSpan.Zero, ct);
            throw new IOException("Injected failure before publication");
#pragma warning disable CS0162
            return true;
#pragma warning restore CS0162
        }, 262_144));
        await using var read = services.CreateAsyncScope(); var db = read.ServiceProvider.GetRequiredService<DataContext>();
        Assert.False(await db.TasteSignals.AnyAsync(s => s.ProfileId == another.Id));
        Assert.False(await db.DiscoveryJobs.AnyAsync(j => j.ProfileId == another.Id));
        Assert.Equal(0, (await db.ListenerProfiles.SingleAsync(p => p.Id == another.Id)).Revision);
    }
    [LocalPostgresMigrationFact]
    public async Task UntrackedSchemaAndTrackedExternalWritesStopAdmissionUntilReconciled()
    {
        var profile = await GuestAsync('a');
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            db.TasteSignals.Add(LinerNotes.Domain.Taste.TasteSignal.ForProfile(profile.Id, null, LinerNotes.Domain.Enums.TasteTargetType.Tag,
                "jazz", 1, LinerNotes.Domain.Enums.TasteSignalSource.InitialSeedManual, "Synthetic external writer."));
            await db.SaveChangesAsync();
        }
        var stale = await Assert.ThrowsAsync<GenerationStoppedException>(() => GuestAsync('b'));
        Assert.Equal("database_inventory_changed", stale.Message);
        await ReconcileAsync(); await GuestAsync('b');
        await using var schema = new NpgsqlConnection(connection); await schema.OpenAsync();
        await using (var create = new NpgsqlCommand("CREATE TABLE public.c7_untracked (id integer)", schema)) await create.ExecuteNonQueryAsync();
        var unknown = await Assert.ThrowsAsync<GenerationStoppedException>(() => GuestAsync('7'));
        Assert.Equal("managed_storage_ledger_missing_or_schema_uncovered", unknown.Message);
        await using var drop = new NpgsqlCommand("DROP TABLE public.c7_untracked", schema); await drop.ExecuteNonQueryAsync();
    }
    [LocalPostgresMigrationFact]
    public async Task CapacityRaceAdmitsOneMutationAndOversizedPublicationRollsBack()
    {
        var first = await GuestAsync('1'); var second = await GuestAsync('2');
        long physical;
        await using (var scope = services.CreateAsyncScope()) physical = (await scope.ServiceProvider.GetRequiredService<IDigestGenerationStore>().ReadStorageAsync()).DatabaseBytes;
        long offset = LocalGenerationStorage.StopBytes - physical - 1_350_000;
        // Controlled occupancy injection; this does not fill the developer's actual filesystem.
        await services.DisposeAsync();
        registrations.AddScoped<LinerNotes.DataAccess.Persistence.Repositories.DigestGenerationStore>();
        registrations.AddScoped<IDigestGenerationStore>(s => new OffsetInventory(s.GetRequiredService<LinerNotes.DataAccess.Persistence.Repositories.DigestGenerationStore>(), offset));
        services = registrations.BuildServiceProvider(); await ReconcileAsync();
        async Task<bool> Publish(Guid profile)
        {
            try { return await WriteAsync((s, ct) => AddPressureRowsAsync(s, profile, 24, ct), estimate: 0); }
            catch (GenerationStoppedException ex) when (ex.Message is "storage_headroom" or "storage_lease_unavailable") { return false; }
        }
        var accepted = await Task.WhenAll(Publish(first.Id), Publish(second.Id));
        Assert.Single(accepted, a => a);
        await using var check = services.CreateAsyncScope();
        Assert.Equal(24, await check.ServiceProvider.GetRequiredService<DataContext>().DiscoveryJobs.CountAsync());
    }
    [LocalPostgresMigrationFact]
    public async Task ActualPayloadExceedingReservationRollsBackJobsAndAccounting()
    {
        var profile = await GuestAsync('3');
        var failure = await Assert.ThrowsAsync<GenerationStoppedException>(() => WriteAsync(async (s, ct) => {
            var context = s.GetRequiredService<DataContext>();
            var changing = await context.ListenerProfiles.AsTracking().SingleAsync(p => p.Id == profile.Id, ct);
            changing.ChangeInputs(0);
            context.TasteSignals.Add(LinerNotes.Domain.Taste.TasteSignal.ForProfile(profile.Id, null, LinerNotes.Domain.Enums.TasteTargetType.Tag,
                "jazz", 1, LinerNotes.Domain.Enums.TasteSignalSource.InitialSeedManual, "Synthetic accepted input in rejected publication."));
            return await AddPressureRowsAsync(s, profile.Id, 80, ct);
        }, estimate: 0));
        Assert.Equal("stored_bytes_exceed_reservation", failure.Message);
        await using (var read = services.CreateAsyncScope())
        {
            var context = read.ServiceProvider.GetRequiredService<DataContext>();
            Assert.Equal(0, await context.DiscoveryJobs.CountAsync());
            Assert.Equal(0, await context.TasteSignals.CountAsync());
            Assert.Equal(0, (await context.ListenerProfiles.SingleAsync(p => p.Id == profile.Id)).Revision);
        }
        await GuestAsync('4'); // Physical rollback growth is measured, never silently deducted from usage.
    }
    [LocalPostgresMigrationFact]
    public async Task DeletionRevokesAnInflightLeaseWithoutOrphanResults()
    {
        var profile = await GuestAsync('5');
        await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id, "delete", new string('a', 64),
            DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow, 32, TimeSpan.Zero, ct));
        var leased = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(DiscoveryJobKind.Generation, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30), 3, ct));
        await using (var deleting = services.CreateAsyncScope()) await deleting.ServiceProvider.GetRequiredService<GuestLifecycle>().ClearAsync(new string('5', 64), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(async (s, ct) => {
            await s.GetRequiredService<IDiscoveryJobStore>().SaveCompletedAsync(leased!, leased!.LeaseToken!.Value, DateTimeOffset.UtcNow, Guid.NewGuid(), null, ct); return true; }));
        await using var read = services.CreateAsyncScope(); var db = read.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Equal(0, await db.DiscoveryJobs.CountAsync()); Assert.Equal(0, await db.DiscoveryBatches.CountAsync());
    }
    private static async Task<bool> AddPressureRowsAsync(IServiceProvider services, Guid profile, int count, CancellationToken ct)
    {
        var db = services.GetRequiredService<DataContext>();
        for (int i = 0; i < count; i++)
        {
            var now = DateTimeOffset.UtcNow;
            var job = new DiscoveryJob(profile, "pressure-" + i, new string('a', 64), DiscoveryJobKind.Lookup, 0, "{}", now);
            var token = Guid.NewGuid(); job.AcquireLease(token, now, TimeSpan.FromSeconds(30), 3);
            string random = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16_000));
            job.Complete(token, now, null, "{\"synthetic\":\"" + random + "\"}"); db.DiscoveryJobs.Add(job);
        }
        await db.SaveChangesAsync(ct); return true;
    }
    private sealed class OffsetInventory(IDigestGenerationStore inner, long offset) : IDigestGenerationStore
    {
        public Task<IAsyncDisposable> AcquireBatchAsync(CancellationToken ct = default) => inner.AcquireBatchAsync(ct);
        public Task<LinerNotes.Domain.Digest.WeeklyDigest?> GetExistingAsync(Guid user, LinerNotes.Domain.Digest.IsoWeek week, CancellationToken ct = default) => inner.GetExistingAsync(user, week, ct);
        public Task<GenerationInputs> ReadInputsAsync(Guid user, CancellationToken ct = default) => inner.ReadInputsAsync(user, ct);
        public async Task<GenerationStorageState> ReadStorageAsync(CancellationToken ct = default)
        { var state = await inner.ReadStorageAsync(ct); return state with { DatabaseBytes = checked(state.DatabaseBytes + offset) }; }
        public Task<LinerNotes.Domain.Digest.WeeklyDigest> PersistWithinAdmissionAsync(Guid user, LinerNotes.Domain.Digest.IsoWeek week, string revision,
            IReadOnlyList<LinerNotes.Domain.Scoring.BaselineAPick> picks, CancellationToken ct = default) =>
            inner.PersistWithinAdmissionAsync(user, week, revision, picks, ct);
        public Task<LinerNotes.Domain.Digest.WeeklyDigest> PersistAsync(Guid user, LinerNotes.Domain.Digest.IsoWeek week, string revision,
            IReadOnlyList<LinerNotes.Domain.Scoring.BaselineAPick> picks, IGenerationStorageReservation reservation, CancellationToken ct = default)
            => inner.PersistAsync(user, week, revision, picks, reservation, ct);
    }

    [LocalPostgresMigrationFact]
    public async Task ExplicitReconciliationDetectsCounterCorruptionWithoutRepairingIt()
    {
        var profile = await GuestAsync('9');
        await WriteAsync(async (s, ct) => {
            var context = s.GetRequiredService<DataContext>();
            context.TasteSignals.Add(LinerNotes.Domain.Taste.TasteSignal.ForProfile(profile.Id, null, LinerNotes.Domain.Enums.TasteTargetType.Tag,
                "jazz", 1, LinerNotes.Domain.Enums.TasteSignalSource.InitialSeedManual, "Synthetic truncate accounting probe."));
            await context.SaveChangesAsync(ct); return true;
        });
        await using (var truncating = new NpgsqlConnection(connection))
        {
            await truncating.OpenAsync();
            await using var truncate = new NpgsqlCommand("TRUNCATE \"TasteSignals\"", truncating); await truncate.ExecuteNonQueryAsync();
        }
        await ReconcileAsync(); // Statement-level accounting also covers an explicit operator TRUNCATE.
        await using (var db = new NpgsqlConnection(connection))
        {
            await db.OpenAsync();
            await using var corrupt = new NpgsqlCommand("UPDATE \"ManagedStorageLedger\" SET \"RowCount\"=\"RowCount\"+1 WHERE \"Id\"=1", db);
            await corrupt.ExecuteNonQueryAsync();
        }
        var failure = await Assert.ThrowsAsync<GenerationStoppedException>(ReconcileAsync);
        Assert.Equal("managed_storage_ledger_mismatch", failure.Message);
    }

    [LocalPostgresMigrationFact]
    public async Task PublicEvidenceLeasesCoalesceFenceAndPreserveExactChecksummedBytes()
    {
        string key = new string('a', 64); var now = DateTimeOffset.UtcNow;
        now = now.AddTicks(-(now.Ticks % 10)); // PostgreSQL timestamps retain microseconds.
        var first = await WriteAsync((s, ct) => s.GetRequiredService<IPublicEvidenceStore>().ClaimAsync(key, PublicEvidenceKind.Pool, now, TimeSpan.FromSeconds(15), ct));
        Assert.NotNull(first);
        Assert.Null(await WriteAsync((s, ct) => s.GetRequiredService<IPublicEvidenceStore>().ClaimAsync(key, PublicEvidenceKind.Pool, now, TimeSpan.FromSeconds(15), ct)));
        var reclaimedAt = first!.ExpiresAtUtc;
        var second = await WriteAsync((s, ct) => s.GetRequiredService<IPublicEvidenceStore>().ClaimAsync(key, PublicEvidenceKind.Pool, reclaimedAt, TimeSpan.FromSeconds(15), ct));
        var document = LinerNotes.Infrastructure.Discovery.PublicEvidenceCodec.Document(key, PublicEvidenceKind.Pool, "listenbrainz",
            LinerNotes.Application.Common.Models.Recommendation.EvidenceOrigin.Synthetic, "fixture-v1", new { Facts = new[] { "Synthetic music" } }, now, now.AddMinutes(10));
        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(async (s, ct) => {
            await s.GetRequiredService<IPublicEvidenceStore>().PublishAsync(first, document, reclaimedAt, 1_048_576, 10, ct); return true; }));
        await WriteAsync(async (s, ct) => { await s.GetRequiredService<IPublicEvidenceStore>().PublishAsync(second!, document, reclaimedAt,
            1_048_576, 10, ct); return true; });
        await using var read = services.CreateAsyncScope();
        var saved = await read.ServiceProvider.GetRequiredService<IPublicEvidenceStore>().FindAsync(key, reclaimedAt, default);
        Assert.Equal(document, saved);
        Assert.Null(await read.ServiceProvider.GetRequiredService<IPublicEvidenceStore>().FindAsync(key, document.ExpiresAtUtc, default));
    }

    [LocalPostgresMigrationFact]
    public async Task KillingBackendBeforeCommitRollsBackRowsLedgerAndAllowsFreshAdmission()
    {
        var entered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = WriteAsync(async (s, ct) => {
            var db = s.GetRequiredService<DataContext>();
            var profile = ListenerProfile.Guest(DateTimeOffset.UtcNow, TimeSpan.FromDays(7)); db.ListenerProfiles.Add(profile); await db.SaveChangesAsync(ct);
            var pg = (NpgsqlConnection)db.Database.GetDbConnection(); entered.SetResult(pg.ProcessID);
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10)); return profile.Id;
        });
        int pid = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using var control = new NpgsqlConnection(admin); await control.OpenAsync();
        await using (var kill = new NpgsqlCommand("SELECT pg_terminate_backend(@pid)", control)) { kill.Parameters.AddWithValue("pid", pid); Assert.True((bool)(await kill.ExecuteScalarAsync())!); }
        bool gone = false;
        for (int i = 0; i < 100; i++)
        {
            await using var check = new NpgsqlCommand("SELECT NOT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid=@pid)", control); check.Parameters.AddWithValue("pid", pid);
            if ((bool)(await check.ExecuteScalarAsync())!) { gone = true; break; }
            await Task.Delay(20);
        }
        Assert.True(gone, "Server rollback must finish before recovery assertions."); release.SetResult();
        await Assert.ThrowsAnyAsync<Exception>(async () => await pending);
        await using (var scope = services.CreateAsyncScope()) Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DataContext>().ListenerProfiles.CountAsync());
        await GuestAsync('8');
    }
}
