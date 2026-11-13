using System.Text.Json;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Taste;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class DiscoveryCoordinationPostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task EveryFavoriteEvidenceStatePersistsExportsAndDeletesWithItsOwner()
    {
        await services.DisposeAsync();
        registrations.AddSingleton(new DiscoveryRuntimeOptions { RunJobs = true });
        services = registrations.BuildServiceProvider();
        await ReconcileAsync();
        var profile = await GuestAsync('a');
        await WriteAsync(async (s, ct) =>
        {
            var db = s.GetRequiredService<DataContext>();
            foreach (int i in Enumerable.Range(0, 50))
            {
                var id = Guid.NewGuid();
                var now = DateTimeOffset.UtcNow;
                var item = new LinerNotes.Application.Common.Interfaces.Catalog.CatalogItemDto(Guid.NewGuid(), id,
                    CatalogKind.Artist, $"Fixture favorite {i:D3}", null, null, null, ["electronic"], "fixture", "Synthetic fixture", Guid.NewGuid(),
                    CatalogOrigin.Synthetic, [], "fixture", "fixture", [new("electronic", 1, "synthetic-artist")], now, now.AddDays(1), new string('a', 64));
                var signal = TasteSignal.ForProfile(profile.Id, null, TasteTargetType.Artist, item.Name, 1, TasteSignalSource.InitialSeedManual, "Synthetic favorite");
                signal.SetCatalogSelection(item.Id, id, JsonSerializer.Serialize(new CatalogSelectionSnapshot("catalog-selection-v1", item)));
                db.TasteSignals.Add(signal);
            }
            await db.SaveChangesAsync(ct);
            return true;
        });
        var batchId = Guid.NewGuid();
        await using (var scope = services.CreateAsyncScope())
        {
            var inputs = (await scope.ServiceProvider.GetRequiredService<IDiscoveryBatchStore>().ReadInputsAsync(profile.Id, DateTimeOffset.UtcNow, 20, default))!;
            var prepared = await scope.ServiceProvider.GetRequiredService<PrepareDiscoveryBatch>().PrepareAsync(inputs, batchId, true, default);
            Assert.NotEmpty(prepared.Picks);
            Assert.All(prepared.Picks, pick => Assert.Equal(50, pick.Breakdown.Snapshot!.Evidence.FavoriteEvidence.Count));
            await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().EnqueueAsync(profile.Id,
                "favorite-evidence", new string('a', 64), DiscoveryJobKind.Generation, 0, "{}", DateTimeOffset.UtcNow, 10, TimeSpan.Zero, ct));
            var lease = await WriteAsync((s, ct) => s.GetRequiredService<IDiscoveryJobStore>().LeaseNextAsync(
                DiscoveryJobKind.Generation, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1), 3, ct));
            await WriteAsync(async (s, ct) =>
            {
                await s.GetRequiredService<IDiscoveryBatchStore>().CompleteAsync(lease!.Id, lease.LeaseToken!.Value,
                    batchId, DiscoveryBatchKind.Initial, prepared.Picks.Select((pick, index) => index == 0
                        ? pick with { Breakdown = pick.Breakdown with { UnknownFields = new() { ["codec-stress-fixture"] = JsonSerializer.SerializeToElement(new string('z', 120_000)) } } }
                        : pick).ToArray(), prepared.CoverageJson, prepared.EvidenceExpiry, DateTimeOffset.UtcNow, ct);
                return true;
            });
        }
        await using (var scope = services.CreateAsyncScope())
        {
            var data = await scope.ServiceProvider.GetRequiredService<GuestLifecycle>().ExportAsync(profile.Id, default);
            Assert.All(data.Recommendations!, pick =>
            {
                Assert.NotNull(pick.ScoreBreakdown.Snapshot!.Evidence.Selection);
                ConnectedSelection.Replay(pick.ScoreBreakdown.Snapshot.Evidence.Selection!);
                Assert.Equal(50, pick.ScoreBreakdown.Snapshot.Evidence.FavoriteEvidence.Count);
                Assert.Equal(50, pick.ScoreBreakdown.Snapshot.Evidence.TasteEvidenceCount);
                Assert.Equal(64, pick.ScoreBreakdown.Snapshot.Evidence.TasteEvidenceSha256!.Length);
            });
            Assert.Contains(data.Recommendations!, pick => pick.ScoreBreakdown.UnknownFields?.TryGetValue("codec-stress-fixture", out var field) == true && field.GetString()!.Length == 120_000);
            Assert.Contains("FavoriteEvidence", data.Batches.Single().CoverageJson!.Value.GetRawText());
            var recent = (await scope.ServiceProvider.GetRequiredService<IDiscoveryBatchStore>().ReadInputsAsync(profile.Id, DateTimeOffset.UtcNow, 20, default))!;
            Assert.NotEmpty(recent.RecentArtists!);
            Assert.All(recent.RecentArtists!, pair => Assert.InRange(pair.Value, 1, 5));
        }
        var other = await GuestAsync('b');
        await using (var scope = services.CreateAsyncScope())
        {
            Assert.Null(await scope.ServiceProvider.GetRequiredService<IDiscoveryReader>().ReadBatchAsync(other.Id, batchId, DateTimeOffset.UtcNow, default));
            var exported = await scope.ServiceProvider.GetRequiredService<GuestLifecycle>().ExportAsync(other.Id, default);
            Assert.Empty(exported.Recommendations!);
            var foreignInputs = (await scope.ServiceProvider.GetRequiredService<IDiscoveryBatchStore>().ReadInputsAsync(other.Id, DateTimeOffset.UtcNow, 20, default))!;
            Assert.Empty(foreignInputs.RecentArtists!);
            await scope.ServiceProvider.GetRequiredService<GuestLifecycle>().ClearAsync(new string('a', 64), default);
        }
        await using var check = services.CreateAsyncScope();
        var verified = check.ServiceProvider.GetRequiredService<DataContext>();
        Assert.False(await verified.WeeklyRecommendations.AnyAsync(p => p.ProfileId == profile.Id));
        Assert.False(await verified.TasteSignals.AnyAsync(p => p.ProfileId == profile.Id));
    }
}
