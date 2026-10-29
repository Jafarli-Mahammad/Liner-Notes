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
