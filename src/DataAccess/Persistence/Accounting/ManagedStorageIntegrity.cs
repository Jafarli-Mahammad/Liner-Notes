using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.DataAccess.DataContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace LinerNotes.DataAccess.Persistence.Accounting;

public sealed class ManagedStorageIntegrity(DataContext db, IUnitOfWork unit, IDigestGenerationStore inventory) : IManagedStorageIntegrity
{
    public async Task<string> ReadPortableSchemaFingerprintAsync(CancellationToken ct)
    {
        var connection=db.Database.GetDbConnection();
        var opened=connection.State!=System.Data.ConnectionState.Open;
        if(opened) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command=connection.CreateCommand();
            command.Transaction=db.Database.CurrentTransaction?.GetDbTransaction();
            // Portable definitions supplement, never replace, runtime OID/file-node checks.
            command.CommandText="""
                SELECT encode(sha256(convert_to(COALESCE(string_agg(definition,E'\n' ORDER BY definition),''),'UTF8')),'hex')
                FROM (
                  SELECT n.nspname||'.'||c.relname||':'||c.relkind::text||':'||
                    COALESCE((SELECT string_agg(a.attname||':'||format_type(a.atttypid,a.atttypmod)||':'||a.attnotnull::text||':'||
                      a.attidentity::text||':'||a.attgenerated::text||':'||COALESCE(pg_get_expr(d.adbin,d.adrelid),'')||':'||
                      COALESCE(coll.collname,''),',' ORDER BY a.attnum)
                      FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum
                      LEFT JOIN pg_collation coll ON coll.oid=a.attcollation
                      WHERE a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped),'')||':'||
                    COALESCE((SELECT string_agg(g.conname||':'||pg_get_constraintdef(g.oid),',' ORDER BY g.conname)
                      FROM pg_constraint g WHERE g.conrelid=c.oid),'')||':'||
                    COALESCE((SELECT string_agg(t.tgname||':'||t.tgenabled::text||':'||pg_get_triggerdef(t.oid),',' ORDER BY t.tgname)
                      FROM pg_trigger t WHERE t.tgrelid=c.oid AND NOT t.tgisinternal),'')||':'||
                    CASE WHEN c.relkind='i' THEN pg_get_indexdef(c.oid) WHEN c.relkind='v' THEN pg_get_viewdef(c.oid) ELSE '' END AS definition
                  FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
                  WHERE c.relkind IN ('r','i','S','v') AND n.nspname IN ('public','identity')
                  UNION ALL
                  SELECT pg_get_functiondef(p.oid) FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
                  WHERE n.nspname IN ('public','identity') AND p.prokind='f'
                ) definitions
                """;
            return (string)(await command.ExecuteScalarAsync(ct) ?? throw new GenerationStoppedException("storage_schema_unavailable"));
        }
        finally { if(opened) await db.Database.CloseConnectionAsync(); }
    }

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
