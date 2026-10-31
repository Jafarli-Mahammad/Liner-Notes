using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Models.Recommendation;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class DiscoveryCoordinationPostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task LargeCompleteScoreReceiptSurvivesJsonbPersistenceAndReplaysExactly()
    {
        var profile = await GuestAsync('a'); var now = DateTimeOffset.UtcNow;
        await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id,
            "large-receipt", new string('a', 64), DiscoveryJobKind.Generation, 0, "{}", now, 10, TimeSpan.Zero, ct));
        var leased = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(
            DiscoveryJobKind.Generation, now, TimeSpan.FromSeconds(60), 3, ct));
        var batch = Guid.NewGuid();
        var track = new LinerNotes.Domain.Catalog.Track("Uncommon track", "Uncommon artist");
        var vector = LinerNotes.Domain.Catalog.WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["jazz"] = 1 });
        var score = new LinerNotes.Domain.Scoring.BaselineAScorer().ScoreForBatch(
            new(track, vector, LinerNotes.Domain.Scoring.ScoreEvidence.Empty), vector, false, batch, new()) with {
            UnknownFields = Enumerable.Range(0, 400).ToDictionary(i => "retained-evidence-" + i,
                i => System.Text.Json.JsonSerializer.SerializeToElement(new { source = "public-provider", identity = i, data = new string('x', 300) }))
        };
        var canonical = System.Text.Json.JsonSerializer.Serialize(score);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(canonical) > 100_000);
        await WriteAsync(async (s, ct) => {
            await s.GetRequiredService<IDiscoveryBatchStore>().CompleteAsync(leased!.Id, leased.LeaseToken!.Value,
                batch, DiscoveryBatchKind.OnDemand, [new(track, score)], "{}", now.AddMinutes(5), DateTimeOffset.UtcNow, ct);
            return true;
        }, 1_048_576);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var loaded = await db.WeeklyRecommendations.AsNoTracking().SingleAsync(r => r.BatchId == batch);
        Assert.Equal(canonical, System.Text.Json.JsonSerializer.Serialize(loaded.ScoreBreakdown));
        Assert.Equal(loaded.ScoreBreakdown.FinalScore, new LinerNotes.Domain.Scoring.BaselineAScorer().Replay(loaded.ScoreBreakdown.Snapshot!).FinalScore);
        var stored = await db.Database.SqlQuery<string>($"SELECT \"ScoreBreakdown\"::text AS \"Value\" FROM \"WeeklyRecommendations\" WHERE \"BatchId\"={batch}").SingleAsync();
        Assert.Contains("linernotes-score-json-gzip-v1", stored);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(stored) < 100_000);
    }

    [LocalPostgresMigrationFact]
    public async Task CompletionClearsExpiredMetadataButRetainsBoundedPublicArtworkProof()
    {
        var now = DateTimeOffset.UtcNow;
        var publicId = Guid.NewGuid(); var syntheticId = Guid.NewGuid();
        await WriteAsync(async (s, ct) => {
            var db = s.GetRequiredService<DataContext>();
            db.Set<RecentRecommendation>().AddRange(Enumerable.Range(0, 1000).Select(_ =>
                new RecentRecommendation(Guid.NewGuid(), "expired", "expired", LinerNotes.Domain.Catalog.CatalogOrigin.Public, "CC0", now.AddHours(-1))));
            await db.SaveChangesAsync(ct);
            db.Set<RecentRecommendation>().AddRange(
                new RecentRecommendation(publicId, "expired", "expired", LinerNotes.Domain.Catalog.CatalogOrigin.Public, "CC0", now.AddHours(-1)),
                new RecentRecommendation(syntheticId, "fixture", "fixture", LinerNotes.Domain.Catalog.CatalogOrigin.Synthetic, "fixture", now.AddHours(-1)));
            return await db.SaveChangesAsync(ct);
        }, 8 * 1024 * 1024);
        var profile = await GuestAsync('a');
        var job = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id,
            "proof-cleanup", new string('a', 64), DiscoveryJobKind.Generation, 0, "{}", now, 10, TimeSpan.Zero, ct));
        var leased = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(
            DiscoveryJobKind.Generation, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(60), 3, ct));
        await WriteAsync(async (s, ct) => {
            await s.GetRequiredService<IDiscoveryBatchStore>().CompleteAsync(job.Id, leased!.LeaseToken!.Value, Guid.NewGuid(),
                DiscoveryBatchKind.OnDemand, [], "{}", now.AddMinutes(5), DateTimeOffset.UtcNow, ct);
            return true;
        });
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var retained = await db.Set<RecentRecommendation>().AsNoTracking().ToArrayAsync();
        Assert.Equal(1000, retained.Length);
        Assert.All(retained, row => { Assert.Empty(row.Title); Assert.Empty(row.ArtistName); Assert.Empty(row.Attribution); });
        Assert.DoesNotContain(retained, row => row.RecordingId == syntheticId);
        Assert.Contains(retained, row => row.RecordingId == publicId);
        Assert.Empty((await scope.ServiceProvider.GetRequiredService<IRecentRecommendations>().ReadAsync(null, 50, DateTimeOffset.UtcNow, default)).Items);
        Assert.Equal(new[] { publicId }, await scope.ServiceProvider.GetRequiredService<IArtworkCatalog>()
            .PreviouslyPublicAsync([publicId, syntheticId], default));

        var sequence = retained.Single(row => row.RecordingId == publicId).Sequence;
        await WriteAsync(async (s, ct) => {
            var database = s.GetRequiredService<DataContext>();
            var publication = new LinerNotes.Domain.Catalog.CatalogPublication(Guid.NewGuid(), "test", "refreshed",
                "https://musicbrainz.org/", LinerNotes.Domain.Catalog.CatalogOrigin.Public, "CC0-1.0", null, "fresh attribution",
                new string('a', 64), 1, 1, now, now.AddMinutes(30), isLookup: true);
            publication.Advance(0, 1); publication.Publish(DateTimeOffset.UtcNow);
            database.CatalogPublications.Add(publication);
            database.CatalogRecords.Add(new(publication.Id, publicId, LinerNotes.Domain.Catalog.CatalogKind.Track, Guid.NewGuid(),
                "Fresh title", "Fresh artist", null, null, "[]", "[]", new string('a', 64)));
            return await database.SaveChangesAsync(ct);
        });
        await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id,
            "proof-refresh", new string('b', 64), DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow, 10, TimeSpan.Zero, ct));
        var refreshedLease = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(
            DiscoveryJobKind.Generation, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(60), 3, ct));
        var batchId = Guid.NewGuid();
        var track = new LinerNotes.Domain.Catalog.Track("Fresh title", "Fresh artist", mbid: publicId.ToString("D"));
        var vector = LinerNotes.Domain.Catalog.WeightedTagVector.FromDictionary(new Dictionary<string, double> { ["jazz"] = 1 });
        var breakdown = new LinerNotes.Domain.Scoring.BaselineAScorer().ScoreForBatch(
            new(track, vector, LinerNotes.Domain.Scoring.ScoreEvidence.Empty), vector, false, batchId, new());
        await WriteAsync(async (s, ct) => {
            await s.GetRequiredService<IDiscoveryBatchStore>().CompleteAsync(refreshedLease!.Id, refreshedLease.LeaseToken!.Value,
                batchId, DiscoveryBatchKind.OnDemand, [new(track, breakdown)], "{}", now.AddMinutes(5), DateTimeOffset.UtcNow, ct);
            return true;
        });
        var refreshed = await db.Set<RecentRecommendation>().AsNoTracking().SingleAsync(row => row.RecordingId == publicId);
        Assert.Equal(sequence, refreshed.Sequence);
        Assert.Equal("Fresh title", refreshed.Title);
        Assert.Equal("Fresh artist", refreshed.ArtistName);
        Assert.Single((await scope.ServiceProvider.GetRequiredService<IRecentRecommendations>().ReadAsync(null, 50, DateTimeOffset.UtcNow, default)).Items);
    }

    [LocalPostgresMigrationFact]
    public async Task ReclaimedCatalogCanRevalidateOnlyRetainedPublicFeedIdentities()
    {
        var publicId = Guid.NewGuid(); var syntheticId = Guid.NewGuid(); var unknown = Guid.NewGuid();
        var expired = DateTimeOffset.UtcNow.AddHours(-1);
        await WriteAsync(async (s, ct) => {
            var db = s.GetRequiredService<DataContext>();
            db.Set<RecentRecommendation>().AddRange(
                new RecentRecommendation(publicId, "public track", "public artist", LinerNotes.Domain.Catalog.CatalogOrigin.Public, "CC0", expired),
                new RecentRecommendation(syntheticId, "fixture track", "fixture artist", LinerNotes.Domain.Catalog.CatalogOrigin.Synthetic, "fixture", expired));
            return await db.SaveChangesAsync(ct);
        });
        await using var scope = services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IArtworkCatalog>();
        Assert.Empty(await catalog.EligibleAsync([publicId, syntheticId, unknown], DateTimeOffset.UtcNow, default));
        Assert.Equal(new[] { publicId }, await catalog.PreviouslyPublicAsync([publicId, syntheticId, unknown], default));
    }

    [LocalPostgresMigrationFact]
    public async Task ArtworkAdmissionOnlyAllowsPublishedUnexpiredPublicRecordings()
    {
        var now = DateTimeOffset.UtcNow;
        var publicId = Guid.NewGuid(); var syntheticId = Guid.NewGuid(); var expiredId = Guid.NewGuid();
        await WriteAsync(async (s, ct) =>
        {
            var db = s.GetRequiredService<DataContext>();
            foreach (var (id, origin, expiry) in new[] {
                (publicId, LinerNotes.Domain.Catalog.CatalogOrigin.Public, now.AddMinutes(30)),
                (syntheticId, LinerNotes.Domain.Catalog.CatalogOrigin.Synthetic, now.AddMinutes(30)),
                (expiredId, LinerNotes.Domain.Catalog.CatalogOrigin.Public, now.AddMinutes(-1)) })
            {
                var publication = new LinerNotes.Domain.Catalog.CatalogPublication(Guid.NewGuid(), "test", "test-v1",
                    "https://musicbrainz.org/", origin, "CC0-1.0", null, "test", new string('a', 64), 1, 1,
                    now.AddHours(-3), expiry, isLookup: true);
                publication.Advance(0, 1); publication.Publish(now.AddHours(-2));
                db.CatalogPublications.Add(publication);
                db.CatalogRecords.Add(new(publication.Id, id, LinerNotes.Domain.Catalog.CatalogKind.Track, Guid.NewGuid(),
                    "Recording", "Artist", null, null, "[]", "[]", new string('a', 64)));
            }
            await db.SaveChangesAsync(ct); return true;
        });
        await using var scope = services.CreateAsyncScope();
        var admitted = await scope.ServiceProvider.GetRequiredService<IArtworkCatalog>()
            .EligibleAsync([publicId, syntheticId, expiredId, Guid.NewGuid()], now, default);
        Assert.Equal(new[] { publicId }, admitted);
        var previouslyPublic = await scope.ServiceProvider.GetRequiredService<IArtworkCatalog>()
            .PreviouslyPublicAsync([publicId, syntheticId, expiredId, Guid.NewGuid()], default);
        Assert.Equal(new[] { publicId, expiredId }.Order(), previouslyPublic.Order());
    }

    [LocalPostgresMigrationFact]
    public async Task DeadlineExpiresDeferredJobsWithoutDependingOnRetryBudget()
    {
        var profile = await GuestAsync('c');
        var now = DateTimeOffset.UtcNow;
        var job = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id,
            "deadline", new string('a', 64), DiscoveryJobKind.Generation, 0, "{}", now, 10, TimeSpan.Zero, ct));
        var lease = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(
            DiscoveryJobKind.Generation, now.AddSeconds(1), TimeSpan.FromSeconds(60), 3, ct));
        await WriteAsync(async (s, ct) => { await s.GetRequiredService<IDiscoveryJobStore>().DeferAsync(job.Id,
            lease!.LeaseToken!.Value, now.AddSeconds(2), TimeSpan.FromSeconds(1), "waiting_for_music", ct); return true; });
        var expiredAt = now.AddMinutes(11);
        await using (var scope = services.CreateAsyncScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<IDiscoveryJobStore>().HasReadyAsync(
                DiscoveryJobKind.Generation, DiscoveryJobLane.Prepared, expiredAt, 3, default));
        Assert.Null(await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(
            DiscoveryJobKind.Generation, expiredAt, TimeSpan.FromSeconds(60), 3, ct)));
        await using var check = services.CreateAsyncScope();
        var saved = await check.ServiceProvider.GetRequiredService<DataContext>().DiscoveryJobs.SingleAsync();
        Assert.Equal(DiscoveryJobState.Failed, saved.State);
        Assert.Equal("job_timeout", saved.ErrorCode);
        Assert.Equal(0, saved.Attempts);
    }

    [LocalPostgresMigrationFact]
    public async Task ImageQuotaCannotEvictRecommendationEvidence()
    {
        var now = DateTimeOffset.UtcNow;
        var payload = System.Text.Json.JsonSerializer.Serialize(new { value = new string('x', 700_000) });
        string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload)));
        foreach (var (letter, kind) in new[] { ('a', PublicEvidenceKind.Pool), ('b', PublicEvidenceKind.Artwork), ('c', PublicEvidenceKind.Artwork) })
            await WriteAsync(async (s, ct) =>
            {
                var store = s.GetRequiredService<IPublicEvidenceStore>();
                string key = new(letter, 64);
                var lease = await store.ClaimAsync(key, kind, now, TimeSpan.FromSeconds(60), ct);
                await store.PublishAsync(lease!, new(key, kind, "test", EvidenceOrigin.Live, "test-v1", payload, hash,
                    now, now.AddMinutes(letter == 'a' ? 1 : 5)), now, 1_048_576, 10, ct);
                return true;
            }, 4_000_000);
        await using var check = services.CreateAsyncScope();
        var evidence = check.ServiceProvider.GetRequiredService<IPublicEvidenceStore>();
        Assert.NotNull(await evidence.FindAsync(new string('a', 64), now, default));
        Assert.Null(await evidence.FindAsync(new string('b', 64), now, default));
        Assert.NotNull(await evidence.FindAsync(new string('c', 64), now, default));
    }

    [LocalPostgresMigrationFact]
    public async Task StoredCoverSurvivesScopeReplacementAndInventoryRemainsValid()
    {
        var id = Guid.NewGuid();
        var until = DateTimeOffset.UtcNow.AddHours(1);
        var cover = new RecordingCover([0xff, 0xd8, 0xff, 0xd9], "image/jpeg", until);
        await using (var scope = services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IStoredArtwork>().SaveAsync(id, cover, default);
        await using var next = services.CreateAsyncScope();
        var restored = await next.ServiceProvider.GetRequiredService<IStoredArtwork>().FindAsync(id, default);
        Assert.NotNull(restored);
        Assert.Equal(cover.Bytes, restored.Bytes);
        Assert.Equal(new DateTimeOffset(until.Ticks - until.Ticks % 10, TimeSpan.Zero), restored.ExpiresAtUtc);
        await next.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
    }
}
