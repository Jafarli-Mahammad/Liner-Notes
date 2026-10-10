using LinerNotes.Application.Common.Interfaces;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.IdentityEntities;
using LinerNotes.DataAccess.Persistence;
using LinerNotes.DataAccess.Persistence.Repositories;
using LinerNotes.Domain.Digest;
using LinerNotes.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed class LocalUnsubscribePostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task PopulatedMigration_IdempotentUpdate_AndPurposeIsolatedRestartTokens()
    {
        var admin = Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES")!;
        var connection = new NpgsqlConnectionStringBuilder(admin);
        Assert.Contains(connection.Host, new[] { "localhost", "127.0.0.1", "::1" });
        string name = "liner_unsubscribe_" + Guid.NewGuid().ToString("N");
        await using var administrator = new NpgsqlConnection(admin);
        await administrator.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", administrator)) await create.ExecuteNonQueryAsync();
        connection.Database = name;
        try
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).Options);
            await db.Database.MigrateAsync("20261002203611_AddRatingToWeeklyRecommendation");
            var id = Guid.NewGuid();
            // Insert with old SQL shape before the additive column exists.
            db.ApplicationUsers.Add(new ApplicationUser { Id=id, Email="local@example.test", UserName="local" });
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Users\" (\"Id\", \"Email\", \"TimeZone\", \"DeliveryDay\", \"DeliveryHourUtc\", \"IsDeleted\", \"CreatedAt\") VALUES ({id}, {"local@example.test"}, {"UTC"}, {"Sunday"}, {8}, {false}, {DateTime.UtcNow})");
            await db.Database.MigrateAsync();
            Assert.Null((await db.Users.SingleAsync()).EmailUnsubscribedAtUtc);
            var store = new UnsubscribeStore(db);
            var time = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
            await store.UnsubscribeAsync(id, time, default);
            await store.UnsubscribeAsync(id, time.AddDays(1), default);
            await store.UnsubscribeAsync(Guid.NewGuid(), time, default);
            Assert.Equal(time, (await db.Users.SingleAsync()).EmailUnsubscribedAtUtc);
            string token;
            using (var protection = new LocalUnsubscribeProtection(connection.ConnectionString))
                token = new UnsubscribeTokenService(protection).Create(id);
            using (var restarted = new LocalUnsubscribeProtection(connection.ConnectionString))
            {
                var tokens = new UnsubscribeTokenService(restarted);
                Assert.True(tokens.TryRead(token, out var read)); Assert.Equal(id, read);
                Assert.False(tokens.TryRead(token[..^5] + "xxxxx", out _));
                Assert.False(tokens.TryRead(new string('x', 4097), out _));
                Assert.DoesNotContain("local@example.test", token);
                var wrongPurpose = new UnsubscribeTokenService(new WrongProtection());
                Assert.False(wrongPurpose.TryRead(token, out _));
            }
            Assert.True(await db.DataProtectionKeys.AnyAsync());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", administrator);
            await drop.ExecuteNonQueryAsync();
        }
    }
    private sealed class WrongProtection : IUnsubscribeTokenProtection
    {
        public string Protect(string value) => value;
        public string Unprotect(string value) => "identity:" + value;
    }
}
