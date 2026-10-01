using LinerNotes.DataAccess.Core;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed class UnitOfWorkTests
{
    [Fact]
    public async Task UnitOfWork_SaveChangesAsync_ShouldPersistPendingChanges()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        using var context = new DataContext(options);
        var uow = new UnitOfWork(context);

        var track = Track.Create("Everything In Its Right Place", "Radiohead", "Kid A");
        await context.Tracks.AddAsync(track);

        var changed = await uow.SaveChangesAsync();
        Assert.Equal(1, changed);

        using var verifyContext = new DataContext(options);
        var persisted = await verifyContext.Tracks.FindAsync(track.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Everything In Its Right Place", persisted.Title);
    }
}
