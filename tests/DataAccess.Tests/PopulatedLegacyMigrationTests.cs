using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed class LocalPostgresMigrationFactAttribute : FactAttribute
{
    public LocalPostgresMigrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES")))
            Skip = "Explicit local PostgreSQL migration test: set LINER_PHASE1_POSTGRES.";
    }
}

public sealed class PopulatedLegacyMigrationTests
{
    [LocalPostgresMigrationFact]
    public async Task AddRatingMigration_PreservesLegacyUsersAndEnforcesForeignKeyForNewRows()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("LINER_PHASE1_POSTGRES")!;
        var admin = new NpgsqlConnectionStringBuilder(adminConnectionString);
        if (admin.Host is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("Migration tests require an explicitly local PostgreSQL database.");

        var databaseName = "liner_migration_" + Guid.NewGuid().ToString("N");
        var databaseCreated = false;
        try
        {
            await using (var connection = new NpgsqlConnection(adminConnectionString))
            {
                await connection.OpenAsync();
                await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
                await create.ExecuteNonQueryAsync();
                databaseCreated = true;
            }

            admin.Database = databaseName;
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(admin.ConnectionString)
                .Options;
            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync("20260930194436_InitialCreate");

            var legacyUserId = Guid.NewGuid();
            var legacyEmail = $"legacy-{legacyUserId:N}@example.test";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Users" ("Id", "Email", "TimeZone", "DeliveryDay", "DeliveryHourUtc", "IsDeleted", "CreatedAt")
                VALUES ({legacyUserId}, {legacyEmail}, {"UTC"}, {"Monday"}, {9}, {false}, {DateTime.UtcNow})
                """);

            await db.Database.MigrateAsync();

            await using (var connection = new NpgsqlConnection(admin.ConnectionString))
            {
                await connection.OpenAsync();
                await using var check = new NpgsqlCommand("""
                    SELECT convalidated
                    FROM pg_constraint
                    WHERE conname = 'FK_Users_Users_Id'
                    """, connection);
                Assert.False((bool)(await check.ExecuteScalarAsync())!, "Legacy rows should leave the compatibility FK unvalidated.");
            }

            var preserved = await db.Database.SqlQuery<Guid>($"SELECT \"Id\" AS \"Value\" FROM \"Users\" WHERE \"Id\" = {legacyUserId}").SingleAsync();
            Assert.Equal(legacyUserId, preserved);

            var orphanId = Guid.NewGuid();
            await Assert.ThrowsAsync<PostgresException>(async () =>
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Users" ("Id", "Email", "TimeZone", "DeliveryDay", "DeliveryHourUtc", "IsDeleted", "CreatedAt")
                    VALUES ({orphanId}, {$"orphan-{orphanId:N}@example.test"}, {"UTC"}, {"Monday"}, {9}, {false}, {DateTime.UtcNow})
                    """));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            if (databaseCreated)
            {
                await using var connection = new NpgsqlConnection(adminConnectionString);
                await connection.OpenAsync();
                await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
                await drop.ExecuteNonQueryAsync();
            }
        }
    }
}
