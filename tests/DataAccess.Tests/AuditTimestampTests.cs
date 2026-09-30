using LinerNotes.DataAccess.Persistence;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed class AuditTimestampTests
{
    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldSetCreatedAt_OnNewEntities()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var beforeTime = DateTime.UtcNow.AddSeconds(-1);
        var user = User.Create("audit@example.com", "UTC");

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var afterTime = DateTime.UtcNow.AddSeconds(1);

        using var queryContext = CreateContext(dbName);
        var loaded = await queryContext.Users.FindAsync(user.Id);

        Assert.NotNull(loaded);
        Assert.InRange(loaded.CreatedAt, beforeTime, afterTime);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldSetLastModifiedAt_OnUpdatedEntities()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = CreateContext(dbName);

        var user = User.Create("schedule@example.com", "UTC");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        using var updateContext = CreateContext(dbName);
        var toUpdate = await updateContext.Users.FindAsync(user.Id);
        Assert.NotNull(toUpdate);

        var beforeUpdate = DateTime.UtcNow.AddSeconds(-1);
        toUpdate.UpdateSchedule(DigestDeliveryDay.Monday, 10, "Europe/Berlin");
        await updateContext.SaveChangesAsync();
        var afterUpdate = DateTime.UtcNow.AddSeconds(1);

        using var queryContext = CreateContext(dbName);
        var loaded = await queryContext.Users.FindAsync(user.Id);

        Assert.NotNull(loaded);
        Assert.NotNull(loaded.LastModifiedAt);
        Assert.InRange(loaded.LastModifiedAt.Value, beforeUpdate, afterUpdate);
    }
}
