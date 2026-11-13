using LinerNotes.DataAccess.Persistence;
using LinerNotes.Domain.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
namespace LinerNotes.DataAccess.Tests;
public sealed partial class CatalogPostgresTests
{
    [Fact]
    public async Task CoverageMigrationRetainsLargeAuditAndRejectsOversizedJson()
    {
        if (_admin.Length == 0) return;
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = ListenerProfile.Guest(Acquired, TimeSpan.FromDays(7));
        db.ListenerProfiles.Add(profile);
        var batch = new DiscoveryBatch(profile.Id, "large-audit", DiscoveryBatchKind.OnDemand, 0, Acquired);
        // Exercise a complete audit above the former 256 KiB bound.
        string coverage = "{\"details\":\"" + new string('x', 442_000) + "\"}";
        batch.RecordEvidence(null, "prepared-public-v1", coverage);
        db.DiscoveryBatches.Add(batch);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Contains(new string('x', 442_000), (await db.DiscoveryBatches.SingleAsync(b => b.Id == batch.Id)).CoverageJson);
        string oversized = "{\"details\":\"" + new string('x', DiscoveryBatch.MaximumCoverageBytes) + "\"}";
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"DiscoveryBatches\" SET \"CoverageJson\"=CAST({oversized} AS jsonb) WHERE \"Id\"={batch.Id}"));
        Assert.Equal("23514", error.SqlState);
    }
}
