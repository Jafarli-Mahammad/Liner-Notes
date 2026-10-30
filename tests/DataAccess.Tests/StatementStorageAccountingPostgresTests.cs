using LinerNotes.Application.Common.Interfaces.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace LinerNotes.DataAccess.Tests;

public sealed partial class DiscoveryCoordinationPostgresTests
{
    [LocalPostgresMigrationFact]
    public async Task StatementAccountingMigrationRollsBackAndReappliesWithoutChangingCharges()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LinerNotes.DataAccess.Persistence.AppDbContext>();
        var migrator = db.GetService<IMigrator>();
        async Task<string> CountersAsync()
        {
            await using var connection = new NpgsqlConnection(this.connection);
            await connection.OpenAsync();
            await using var query = new NpgsqlCommand("SELECT to_jsonb(l)::text FROM \"ManagedStorageLedger\" l", connection);
            return (string)(await query.ExecuteScalarAsync())!;
        }
        string before = await CountersAsync();
        await migrator.MigrateAsync("20261029191030_AddManagedStorageCommitWitness");
        Assert.Equal(before, await CountersAsync());
        // The application targets the current schema. Compare raw counters while downgraded;
        // run the full invariant verifier again after reapplying the migrations.
        await migrator.MigrateAsync();
        Assert.Equal(before, await CountersAsync());
        await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        await scope.ServiceProvider.GetRequiredService<LinerNotes.Application.Common.Interfaces.Recommendation.IDigestGenerationStore>().ReadStorageAsync();
    }

    [LocalPostgresMigrationFact]
    public async Task StatementAccountingPreservesExactChargesRevisionsUpsertAndRollback()
    {
        await using var sql = new NpgsqlConnection(connection);
        await sql.OpenAsync();
        async Task Execute(string text)
        {
            await using var command = new NpgsqlCommand(text, sql);
            await command.ExecuteNonQueryAsync();
        }
        async Task<(long Revision, long Rows)> State()
        {
            await using var command = new NpgsqlCommand("SELECT \"Revision\",\"RowCount\" FROM \"ManagedStorageLedger\" WHERE \"Id\"=1", sql);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            return (reader.GetInt64(0), reader.GetInt64(1));
        }
        async Task Verify()
        {
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IManagedStorageIntegrity>().VerifyAsync(default);
        }
        await Execute("""
            CREATE TABLE public.ledger_probe (id integer PRIMARY KEY, value text NOT NULL);
            CREATE TRIGGER liner_storage_delta AFTER INSERT ON public.ledger_probe REFERENCING NEW TABLE AS liner_new_rows FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_statement_delta();
            CREATE TRIGGER liner_storage_update AFTER UPDATE ON public.ledger_probe REFERENCING OLD TABLE AS liner_old_rows NEW TABLE AS liner_new_rows FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_statement_delta();
            CREATE TRIGGER liner_storage_delete AFTER DELETE ON public.ledger_probe REFERENCING OLD TABLE AS liner_old_rows FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_statement_delta();
            CREATE TRIGGER liner_storage_truncate BEFORE TRUNCATE ON public.ledger_probe FOR EACH STATEMENT EXECUTE FUNCTION liner_managed_storage_delta();
            """);
        var before = await State();
        await Execute("INSERT INTO public.ledger_probe SELECT n,repeat('x',n) FROM generate_series(1,1000) n");
        Assert.Equal((before.Revision + 1000, before.Rows + 1000), await State());
        await Verify();
        await Execute("UPDATE public.ledger_probe SET value=value WHERE id<=10");
        Assert.Equal((before.Revision + 1010, before.Rows + 1000), await State());
        await Execute("UPDATE public.ledger_probe SET value='zero' WHERE id<0");
        Assert.Equal((before.Revision + 1010, before.Rows + 1000), await State());
        await Execute("INSERT INTO public.ledger_probe VALUES (1,'replaced'),(1001,'new') ON CONFLICT (id) DO UPDATE SET value=excluded.value");
        Assert.Equal((before.Revision + 1012, before.Rows + 1001), await State());
        await Verify();
        await Execute("BEGIN; DELETE FROM public.ledger_probe; ROLLBACK;");
        Assert.Equal((before.Revision + 1012, before.Rows + 1001), await State());
        await Execute("DELETE FROM public.ledger_probe WHERE id<=500");
        Assert.Equal((before.Revision + 1512, before.Rows + 501), await State());
        await Verify();
        await Execute("TRUNCATE public.ledger_probe");
        Assert.Equal((before.Revision + 1513, before.Rows), await State());
        await Verify();
        await Execute("DROP TABLE public.ledger_probe");
    }
}
