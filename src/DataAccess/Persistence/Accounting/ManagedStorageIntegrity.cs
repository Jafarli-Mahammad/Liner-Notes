using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.DataAccess.DataContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace LinerNotes.DataAccess.Persistence.Accounting;

public sealed class ManagedStorageIntegrity(DataContext db, IUnitOfWork unit, IDigestGenerationStore inventory) : IManagedStorageIntegrity
{
    private static readonly HashSet<string> Core = new(StringComparer.Ordinal) {
        "public.Tracks", "public.TasteSignals", "public.WeeklyDigests", "public.WeeklyRecommendations",
        "public.ListenerProfiles", "public.DiscoveryBatches", "public.DiscoveryJobs", "public.GuestSessions" };
    public async Task VerifyAsync(CancellationToken ct)
    {
        await using var batch = await inventory.AcquireBatchAsync(ct);
        await unit.ExecuteInTransactionAsync(async token =>
        {
            var tables = new List<(string Identity, string Sql)>();
            var connection = db.Database.GetDbConnection();
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandText = """
                    SELECT schemaname||'.'||tablename,quote_ident(schemaname)||'.'||quote_ident(tablename)
                    FROM pg_tables WHERE schemaname IN ('public','identity') AND tablename NOT IN ('__EFMigrationsHistory','ManagedStorageLedger')
                    ORDER BY schemaname,tablename
                    """;
                await using var reader = await command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                {
                    if (tables.Count >= 256) throw new GenerationStoppedException("managed_storage_schema_uncovered");
                    tables.Add((reader.GetString(0), reader.GetString(1)));
                }
            }
            if (tables.Count == 0) throw new GenerationStoppedException("managed_storage_schema_uncovered");
            // The explicit integrity operation may scan data; ordinary admission reads counters only.
            await db.Database.ExecuteSqlRawAsync("LOCK TABLE " + string.Join(',', tables.Select(t => t.Sql)) + " IN SHARE ROW EXCLUSIVE MODE", token);
            await inventory.ReadStorageAsync(token); // Also require every table's accounting trigger and origin-mode execution.
            long bytes = 0, core = 0, rows = 0;
            foreach (var table in tables)
            {
                await using var command = connection.CreateCommand(); command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandText = "SELECT COALESCE(sum(octet_length(to_jsonb(t)::text)+512),0)::bigint,count(*) FROM " + table.Sql + " t";
                await using var reader = await command.ExecuteReaderAsync(token); await reader.ReadAsync(token);
                long measured = reader.GetInt64(0); bytes = checked(bytes + measured); rows = checked(rows + reader.GetInt64(1));
                if (Core.Contains(table.Identity)) core = checked(core + measured);
            }
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandText = "SELECT \"LogicalBytes\",\"CoreLogicalBytes\",\"RowCount\" FROM \"ManagedStorageLedger\" WHERE \"Id\"=1 FOR UPDATE";
                await using var reader = await command.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token) || reader.GetInt64(0) != bytes || reader.GetInt64(1) != core || reader.GetInt64(2) != rows)
                    throw new GenerationStoppedException("managed_storage_ledger_mismatch");
            }
            return true;
        }, ct);
    }
}
