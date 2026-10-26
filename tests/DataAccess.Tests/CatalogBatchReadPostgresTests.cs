using System.Data.Common;
using LinerNotes.Application.Common.Interfaces.Catalog;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.Persistence.Repositories;
using LinerNotes.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class CatalogPostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task EligibleItemReadUsesOneQueryAndRejectsExpiredMetadata()
    {
        await using var scope = _services.CreateAsyncScope();
        await ImportAsync(scope.ServiceProvider, Enumerable.Range(0, 10).Select(i =>
            new CatalogArtifactRow(Guid.NewGuid(), CatalogKind.Artist, $"Batch read fixture {i}", [], [], [new("jazz", 1)])).ToArray());
        var original = scope.ServiceProvider.GetRequiredService<DataContext>();
        var ids = await original.CatalogRecords.OrderBy(r => r.Id).Select(r => r.Id).ToArrayAsync();
        var id = ids[0];
        var counter = new CatalogQueryCounter();
        await using var db = new DataContext(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(original.Database.GetConnectionString()).AddInterceptors(counter).Options);
        var reader = new CatalogReader(db);
        var item = await reader.GetAsync(id, DateTimeOffset.UtcNow, default);
        Assert.NotNull(item);
        Assert.Equal(1, counter.Reads);
        Assert.Equal("jazz", Assert.Single(item.Tags));
        Assert.Empty(db.ChangeTracker.Entries());
        counter.Reads = 0;
        var requested = ids.Reverse().Append(Guid.NewGuid()).Append(id).ToArray();
        var many = await reader.GetManyAsync(requested, DateTimeOffset.UtcNow, default);
        Assert.Equal(ids.Reverse().Append(id), many.Select(i => i.Id));
        Assert.Equal(1, counter.Reads);
        counter.Reads = 0;
        Assert.Empty(await reader.GetManyAsync([], DateTimeOffset.UtcNow, default));
        Assert.Equal(0, counter.Reads);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetManyAsync([id], DateTimeOffset.UtcNow, cancelled.Token));
        await original.CatalogPublications.ExecuteUpdateAsync(s => s.SetProperty(p => p.ExpiresAtUtc, DateTimeOffset.UtcNow.AddSeconds(-1)));
        counter.Reads = 0;
        Assert.Null(await reader.GetAsync(id, DateTimeOffset.UtcNow, default));
        Assert.Equal(1, counter.Reads);
        Assert.Empty(await reader.GetManyAsync([id, Guid.NewGuid()], DateTimeOffset.UtcNow, default));
    }

    private sealed class CatalogQueryCounter : DbCommandInterceptor
    {
        public int Reads;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Reads++;
            return ValueTask.FromResult(result);
        }
    }
}
