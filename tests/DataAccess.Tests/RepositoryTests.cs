using LinerNotes.DataAccess.Persistence;
using LinerNotes.DataAccess.Persistence.Repositories;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using LinerNotes.Domain.Scoring;
using LinerNotes.Domain.Taste;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed class RepositoryTests
{
    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task UserRepository_GetUsersDueForDigestAsync_ReturnsOnlyDueUsers()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var dueUser = User.Create("due@example.com", "UTC");
        dueUser.SetNextDigestAt(DateTime.UtcNow.AddMinutes(-10));

        var futureUser = User.Create("future@example.com", "UTC");
        futureUser.SetNextDigestAt(DateTime.UtcNow.AddHours(5));

        var unscheduledUser = User.Create("unscheduled@example.com", "UTC");

        var repo = new UserRepository(context);
        await repo.AddAsync(dueUser);
        await repo.AddAsync(futureUser);
        await repo.AddAsync(unscheduledUser);
        await context.SaveChangesAsync();

        var dueList = await repo.GetUsersDueForDigestAsync(DateTime.UtcNow);

        Assert.Single(dueList);
        Assert.Equal("due@example.com", dueList[0].Email);
    }

    [Fact]
    public async Task WeeklyDigestRepository_ExistsForUserAndWeekAsync_ReturnsExpectedResult()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var userId = Guid.NewGuid();
        var week = new IsoWeek(2026, 39);
        var digest = WeeklyDigest.Create(
            userId,
            week,
            new DateTime(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 27, 23, 59, 59, DateTimeKind.Utc));

        var repo = new WeeklyDigestRepository(context);
        await repo.AddAsync(digest);
        await context.SaveChangesAsync();

        var exists = await repo.ExistsForUserAndWeekAsync(userId, week);
        var notExists = await repo.ExistsForUserAndWeekAsync(userId, new IsoWeek(2026, 40));

        Assert.True(exists);
        Assert.False(notExists);
    }

    [Fact]
    public async Task TasteSignalRepository_GetByUserIdAsync_ReturnsUserSignals()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var signal1 = TasteSignal.CreateSeedTag(user1, "ambient", 0.9, "Seed 1");
        var signal2 = TasteSignal.CreateSeedArtist(user1, "Brian Eno", 1.0, "Seed 2");
        var signal3 = TasteSignal.CreateSeedTag(user2, "thrash metal", 0.9, "Seed 3");

        var repo = new TasteSignalRepository(context);
        await repo.AddRangeAsync(new[] { signal1, signal2, signal3 });
        await context.SaveChangesAsync();

        var user1Signals = await repo.GetByUserIdAsync(user1);

        Assert.Equal(2, user1Signals.Count);
        Assert.All(user1Signals, s => Assert.Equal(user1, s.UserId));
    }

    [Fact]
    public async Task TrackRepository_FindByArtistAndTitleAsync_MatchesCaseInsensitively()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var track = Track.Create("Heroes", "David Bowie", "Heroes", "mbid-bowie-1");

        var repo = new TrackRepository(context);
        await repo.AddAsync(track);
        await context.SaveChangesAsync();

        var found = await repo.FindByArtistAndTitleAsync("david bowie", "heroes");
        var notFound = await repo.FindByArtistAndTitleAsync("David Bowie", "Starman");

        Assert.NotNull(found);
        Assert.Equal(track.Id, found.Id);
        Assert.Null(notFound);
    }
}
