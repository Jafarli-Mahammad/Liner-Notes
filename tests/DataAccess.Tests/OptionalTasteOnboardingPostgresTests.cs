using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
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
    public async Task OptionalTasteRoundTripsEveryTagExportsStateAndRejectsStaleRevisions()
    {
        var profile = await GuestAsync('a');
        await using var scope = services.CreateAsyncScope();
        var save = scope.ServiceProvider.GetRequiredService<SaveTasteOnboarding>();
        var tags = TasteVocabulary.Entries.Select(t => t.Id).ToArray();
        Assert.True(tags.Length > 200);
        long revision = await save.SaveAsync(profile.Id, new(0, new(Step: "review", Goal: "new-artists", Vocals: "no-preference", AllStyles: true), new([], tags)), default);
        var reader = scope.ServiceProvider.GetRequiredService<IDiscoveryReader>();
        var page = await reader.ReadAsync(profile.Id, DateTimeOffset.UtcNow, TimeSpan.Zero, default);
        Assert.Equal(tags.Order(), page!.Tags.Order()); Assert.True(page.Onboarding!.AllStyles);
        Assert.Equal("no-preference", page.Onboarding.Vocals);
        var exported = await scope.ServiceProvider.GetRequiredService<GuestLifecycle>().ExportAsync(profile.Id, default);
        Assert.Equal(tags.Length, exported.TasteSignals!.Count);
        Assert.Equal(page.Onboarding, OnboardingState.Read(exported.Profile!.OnboardingJson));
        await Assert.ThrowsAsync<InvalidOperationException>(() => save.SaveAsync(profile.Id, new(0, new(), new([], [])), default));
        await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Empty(await db.DiscoveryJobs.ToArrayAsync());
        Assert.Equal(revision, page.Revision);
    }

    [LocalPostgresMigrationFact]
    public async Task EmptySetupIsSavedWithoutGeneratingAndGuestDeletionRemovesIt()
    {
        var profile = await GuestAsync('b');
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SaveTasteOnboarding>().SaveAsync(profile.Id,
            new(0, new(Step: "complete", Goal: "skipped", Exploration: "skipped", Vocals: "skipped"), new([], [])), default);
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        Assert.Empty(await db.TasteSignals.ToArrayAsync()); Assert.Empty(await db.DiscoveryJobs.ToArrayAsync());
        Assert.NotNull((await db.ListenerProfiles.SingleAsync()).OnboardingJson);
        await scope.ServiceProvider.GetRequiredService<GuestLifecycle>().ClearAsync(new string('b', 64), default);
        Assert.Empty(await db.ListenerProfiles.ToArrayAsync());
        await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }

    [LocalPostgresMigrationFact]
    public async Task PublicFeedUsesEligibleCatalogMetadataAndIsAtomicIndependentAndDeduplicated()
    {
        var profile = await GuestAsync('c');
        var recording = Guid.NewGuid(); var publicationId = Guid.NewGuid();
        await WriteAsync(async (s, ct) =>
        {
            var db = s.GetRequiredService<DataContext>();
            var now = DateTimeOffset.UtcNow;
            var publication = new CatalogPublication(publicationId, "generated-fixture", "feed-fixture-v1", "fixture:feed",
                CatalogOrigin.Synthetic, "synthetic-fixture", null, "Public fixture music", new string('a', 64), 100, 1, now, now.AddHours(1));
            publication.Advance(0, 1); publication.Publish(now);
            db.CatalogPublications.Add(publication);
            var head = new CatalogHead(); head.Publish(publicationId); db.CatalogHeads.Add(head);
            db.CatalogRecords.Add(new CatalogRecord(publicationId, recording, CatalogKind.Track, recording,
                "Public catalog title", "Public catalog credit", "Recording version", null, "[]", "[]", new string('a', 64)));
            await db.SaveChangesAsync(ct); return true;
        });
        await using (var selectionScope = services.CreateAsyncScope())
        {
            var recordId = CatalogText.RecordId(publicationId, CatalogKind.Track, recording);
            await selectionScope.ServiceProvider.GetRequiredService<SaveTasteOnboarding>().SaveAsync(profile.Id,
                new(0, new(Step: "complete"), new([recordId], [])), default);
            var page = await selectionScope.ServiceProvider.GetRequiredService<IDiscoveryReader>().ReadAsync(profile.Id, DateTimeOffset.UtcNow, TimeSpan.Zero, default);
            Assert.Equal("Track", Assert.Single(page!.Favorites).Kind);
        }
        var job = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id,
            "feed-fixture", new string('f', 64), DiscoveryJobKind.Generation, 1, "{}", DateTimeOffset.UtcNow, 10, TimeSpan.Zero, ct));
        var lease = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(DiscoveryJobKind.Generation,
            DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), 3, ct));
        var batchId = Guid.NewGuid();
        var privateTrack = Track.Create("Private title must never be copied", "Private credit", mbid: recording.ToString("D"));
        var breakdown = new BaselineAScorer().ScoreForBatch(new(privateTrack, WeightedTagVector.Empty, ScoreEvidence.Empty),
            WeightedTagVector.Empty, false, batchId, new());
        async Task<bool> Complete(IServiceProvider s, CancellationToken ct)
        {
            await s.GetRequiredService<IDiscoveryBatchStore>().CompleteAsync(job.Id, lease!.LeaseToken!.Value, batchId,
                DiscoveryBatchKind.Initial, [new(privateTrack, breakdown)], "{}", null, DateTimeOffset.UtcNow, ct);
            return true;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(async (s, ct) => { await Complete(s, ct); return await Task.FromException<bool>(new InvalidOperationException("rollback fixture")); }));
        await using var scope = services.CreateAsyncScope();
        var feed = scope.ServiceProvider.GetRequiredService<IRecentRecommendations>();
        Assert.Empty((await feed.ReadAsync(null, 20, DateTimeOffset.UtcNow, default)).Items);
        await WriteAsync(Complete);
        var item = Assert.Single((await feed.ReadAsync(null, 20, DateTimeOffset.UtcNow, default)).Items);
        Assert.Equal("Public catalog title", item.Title); Assert.Equal("Public catalog credit", item.ArtistName);
        Assert.Equal("Synthetic", item.Origin);
        await scope.ServiceProvider.GetRequiredService<GuestLifecycle>().ClearAsync(new string('c', 64), default);
        Assert.Single((await feed.ReadAsync(null, 20, DateTimeOffset.UtcNow, default)).Items);
        var secondProfile = await GuestAsync('d');
        job = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(secondProfile.Id,
            "feed-repeat", new string('e', 64), DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow, 10, TimeSpan.Zero, ct));
        lease = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(DiscoveryJobKind.Generation,
            DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2), 3, ct));
        batchId = Guid.NewGuid();
        breakdown = new BaselineAScorer().ScoreForBatch(new(privateTrack, WeightedTagVector.Empty, ScoreEvidence.Empty),
            WeightedTagVector.Empty, false, batchId, new());
        await WriteAsync(Complete);
        Assert.Equal(item.Sequence, Assert.Single((await feed.ReadAsync(null, 20, DateTimeOffset.UtcNow, default)).Items).Sequence);
        var entity = scope.ServiceProvider.GetRequiredService<DataContext>().Model.FindEntityType(typeof(RecentRecommendation))!;
        Assert.Empty(entity.GetForeignKeys());
        Assert.Equal(new[] { "ArtistName", "Attribution", "MetadataExpiresAtUtc", "Origin", "RecordingId", "Sequence", "Title" }, entity.GetProperties().Select(p => p.Name).Order());
        await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }
}
