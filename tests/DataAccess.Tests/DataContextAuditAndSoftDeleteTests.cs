using System.Security.Claims;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.IdentityEntities;
using LinerNotes.Domain.Digest;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed class DataContextAuditAndSoftDeleteTests
{
    private sealed class FakeHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }

        public FakeHttpContextAccessor(Guid userId)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, "audit-user@example.com")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            };
        }
    }

    [Fact]
    public async Task DataContext_ShouldPopulateCreatedByAndCreatedAt_FromCurrentPrincipal()
    {
        var dbName = Guid.NewGuid().ToString();
        var currentUserId = Guid.NewGuid();
        var httpAccessor = new FakeHttpContextAccessor(currentUserId);

        var options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        using var context = new DataContext(options, httpAccessor);
        var user = User.Create("creator@example.com", "UTC");

        context.Users.Add(user);
        await context.SaveChangesAsync();

        using var verifyContext = new DataContext(options);
        var loaded = await verifyContext.Users.FindAsync(user.Id);

        Assert.NotNull(loaded);
        Assert.Equal(currentUserId, loaded.CreatedBy);
        Assert.True(loaded.CreatedAt <= DateTime.UtcNow);
        Assert.False(loaded.IsDeleted);
    }

    [Fact]
    public async Task DataContext_SoftDelete_ShouldSetDeletedByAndDeletedAt_AndBeFiltered()
    {
        var dbName = Guid.NewGuid().ToString();
        var currentUserId = Guid.NewGuid();
        var httpAccessor = new FakeHttpContextAccessor(currentUserId);

        var options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        using var context = new DataContext(options, httpAccessor);
        var user = User.Create("to-delete@example.com", "UTC");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Soft delete via Remove
        context.Users.Remove(user);
        await context.SaveChangesAsync();

        // Verify entity is excluded by default query filter
        using var queryContext = new DataContext(options);
        var activeUsers = await queryContext.Users.ToListAsync();
        Assert.Empty(activeUsers);

        // Verify entity still exists when ignoring query filters with audit trail set
        var softDeleted = await queryContext.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.NotNull(softDeleted);
        Assert.True(softDeleted.IsDeleted);
        Assert.Equal(currentUserId, softDeleted.DeletedBy);
        Assert.NotNull(softDeleted.DeletedAt);
    }

    [Fact]
    public async Task DataContext_ApplicationUser_ShouldPersistInIdentitySchema()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<DataContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        using var context = new DataContext(options);
        var identityUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "listener_one",
            Email = "listener_one@example.com"
        };

        context.ApplicationUsers.Add(identityUser);
        await context.SaveChangesAsync();

        using var verifyContext = new DataContext(options);
        var loaded = await verifyContext.ApplicationUsers.FirstOrDefaultAsync(u => u.Id == identityUser.Id);

        Assert.NotNull(loaded);
        Assert.Equal("listener_one", loaded.UserName);
        Assert.Equal("listener_one@example.com", loaded.Email);
    }
}
