using LinerNotes.Application.Common.Exceptions;
using LinerNotes.DataAccess.Core;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed class AsyncRepositoryTests
{
    private static DataContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new DataContext(options);
    }

    [Fact]
    public async Task AsyncRepository_AddAndGetAsync_ShouldPersistAndRetrieveEntity()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var repo = new AsyncRepository<Track>(context);

        var track = Track.Create("Reckoner", "Radiohead", "In Rainbows", "mbid-rec-1");
        await repo.AddAsync(track);
        await context.SaveChangesAsync();

        var retrieved = await repo.GetAsync(t => t.Id == track.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("Reckoner", retrieved.Title);
        Assert.Equal("Radiohead", retrieved.ArtistName);
    }

    [Fact]
    public async Task AsyncRepository_GetAsync_ShouldThrowNotFoundException_WhenMissing()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var repo = new AsyncRepository<Track>(context);

        var missingId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => repo.GetAsync(t => t.Id == missingId));

        Assert.Contains(nameof(Track), exception.Message);
    }

    [Fact]
    public async Task AsyncRepository_FindAsync_ShouldReturnNull_WhenMissing()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var repo = new AsyncRepository<Track>(context);

        var missingId = Guid.NewGuid();
        var found = await repo.FindAsync(t => t.Id == missingId);

        Assert.Null(found);
    }

    [Fact]
    public async Task AsyncRepository_ExistsAsync_ShouldReturnTrue_WhenMatches()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var repo = new AsyncRepository<Track>(context);

        var track = Track.Create("Karma Police", "Radiohead", "OK Computer", "mbid-kp-1");
        await repo.AddAsync(track);
        await context.SaveChangesAsync();

        var exists = await repo.ExistsAsync(t => t.Title == "Karma Police");
        var notExists = await repo.ExistsAsync(t => t.Title == "Creep");

        Assert.True(exists);
        Assert.False(notExists);
    }

    [Fact]
    public async Task AsyncRepository_GetAllAsync_WithFilter_ShouldReturnMatchingItems()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var repo = new AsyncRepository<Track>(context);

        await repo.AddAsync(Track.Create("Song 1", "Artist A"));
        await repo.AddAsync(Track.Create("Song 2", "Artist A"));
        await repo.AddAsync(Track.Create("Song 3", "Artist B"));
        await context.SaveChangesAsync();

        var artistATracks = await repo.GetAllAsync(t => t.ArtistName == "Artist A");

        Assert.Equal(2, artistATracks.Count);
        Assert.All(artistATracks, t => Assert.Equal("Artist A", t.ArtistName));
    }

    [Fact]
    public async Task AsyncRepository_EditAndRemove_ShouldUpdateAndRemoveEntity()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);
        var repo = new AsyncRepository<User>(context);

        var user = User.Create("edit@example.com", "UTC");
        await repo.AddAsync(user);
        await context.SaveChangesAsync();

        // Edit
        user.UpdateSchedule(DigestDeliveryDay.Saturday, 12, "America/New_York");
        await repo.EditAsync(user);
        await context.SaveChangesAsync();

        var updated = await repo.FindAsync(u => u.Id == user.Id);
        Assert.NotNull(updated);
        Assert.Equal(DigestDeliveryDay.Saturday, updated.DeliveryDay);
        Assert.Equal("America/New_York", updated.TimeZone);

        // Remove (Soft delete via DataContext interceptor)
        repo.Remove(updated);
        await context.SaveChangesAsync();

        var afterRemove = await repo.FindAsync(u => u.Id == user.Id);
        Assert.Null(afterRemove); // Excluded by soft-delete query filter
    }
}
