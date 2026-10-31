using System.Globalization;
using System.Text;
using System.Text.Json;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.DataAccess.Persistence.Accounting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class ManagedStorageCommitStore(DataContext db, IUnitOfWork unit, IDigestGenerationStore inventory) : IManagedStorageCommitStore
{
    public async Task<ManagedStorageCommit?> ReadAsync(CancellationToken ct)
    {
        try
        {
            var record = await db.Set<ManagedStorageCommitRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, ct);
            return record is { Initialized: true } ? Convert(record) : null;
        }
        catch (PostgresException error) when (error.SqlState is PostgresErrorCodes.UndefinedTable or PostgresErrorCodes.UndefinedColumn)
        {
            // An older deployment must not be mistaken for an empty, eligible inventory.
            throw new GenerationStoppedException("storage_schema_upgrade_required");
        }
    }

    public async Task<ManagedStorageCommit> RecordAsync(Guid commitId, StoragePublicationPlan plan, string schemaFingerprint, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null || commitId == Guid.Empty || plan.MeasuredAtUtc.Offset != TimeSpan.Zero ||
            plan.RootFingerprint.Length != 64 || schemaFingerprint.Length != 32 ||
            new[] { plan.BeforeInventoryJson, plan.ArtifactCategoriesJson, plan.OwnedChangesJson }.Any(value => value.Length > 131072))
            throw new GenerationStoppedException("storage_commit_plan_invalid");
        // Keep routine witness updates at a stable logical charge. Trailing JSON whitespace is
        // semantically inert; padding includes PostgreSQL's JSON-string escaping contribution.
        // Whole-second operational timestamps avoid variable fractional widths in to_jsonb.
        plan = plan with
        {
            BeforeInventoryJson = PadJson(plan.BeforeInventoryJson, 4096),
            ArtifactCategoriesJson = PadJson(plan.ArtifactCategoriesJson, 2048),
            OwnedChangesJson = PadJson(plan.OwnedChangesJson, 1024),
            MeasuredAtUtc = DateTimeOffset.FromUnixTimeSeconds(plan.MeasuredAtUtc.ToUnixTimeSeconds())
        };
        // Fixed-width decimal encodings make the final row charge independent of counter values.
        // Compute the exact JSON charge of the candidate, then perform one final managed UPDATE.
        int changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            WITH candidate AS (
              SELECT r AS previous, jsonb_populate_record(NULL::"ManagedStorageCommit", to_jsonb(r) ||
                jsonb_build_object('Initialized',true,'CommitId',{commitId},'BeforeInventoryJson',{plan.BeforeInventoryJson},
                  'ArtifactCategoriesJson',{plan.ArtifactCategoriesJson},'OwnedChangesJson',{plan.OwnedChangesJson},
                  'RootFingerprint',{plan.RootFingerprint},'MeasuredAtUtc',{plan.MeasuredAtUtc},'SchemaFingerprint',{schemaFingerprint},
                  'ExpectedRevision','00000000000000000000','ExpectedLogicalBytes','00000000000000000000',
                  'ExpectedCoreRevision','00000000000000000000','ExpectedCoreLogicalBytes','00000000000000000000',
                  'ExpectedRowCount','00000000000000000000')) AS next
              FROM "ManagedStorageCommit" r WHERE r."Id"=1
            ), counters AS (
              SELECT l.*,c.next,octet_length(to_jsonb(c.next)::text)-octet_length(to_jsonb(c.previous)::text) AS delta
              FROM "ManagedStorageLedger" l CROSS JOIN candidate c WHERE l."Id"=1
            )
            UPDATE "ManagedStorageCommit" r SET
              "Initialized"=true,"CommitId"={commitId},"BeforeInventoryJson"={plan.BeforeInventoryJson},
              "ArtifactCategoriesJson"={plan.ArtifactCategoriesJson},"OwnedChangesJson"={plan.OwnedChangesJson},
              "RootFingerprint"={plan.RootFingerprint},"MeasuredAtUtc"={plan.MeasuredAtUtc},"SchemaFingerprint"={schemaFingerprint},
              "ExpectedRevision"=lpad((c."Revision"+1)::text,20,'0'),
              "ExpectedLogicalBytes"=lpad((c."LogicalBytes"+c.delta)::text,20,'0'),
              "ExpectedCoreRevision"=lpad(c."CoreRevision"::text,20,'0'),
              "ExpectedCoreLogicalBytes"=lpad(c."CoreLogicalBytes"::text,20,'0'),
              "ExpectedRowCount"=lpad(c."RowCount"::text,20,'0')
            FROM counters c WHERE r."Id"=1
            """, ct);
        if (changed != 1) throw new GenerationStoppedException("storage_commit_witness_missing");
        var witness = await ReadAsync(ct) ?? throw new GenerationStoppedException("storage_commit_witness_missing");
        // The deferred database constraint checks all final counters, including this
        // row's contribution, atomically at commit. Admission reads the resulting state
        // for its quota check; repeating those queries here adds no additional guarantee.
        return witness;
    }

    public Task<GenerationStorageState> EstablishAsync(Guid commitId, StoragePublicationPlan plan, CancellationToken ct) =>
        unit.ExecuteInTransactionAsync(async token =>
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1280200758)", token);
            // Lock counters before deriving the witness's own exact final contribution.
            await db.Database.ExecuteSqlRawAsync("SELECT 1 FROM \"ManagedStorageLedger\" WHERE \"Id\"=1 FOR UPDATE", token);
            var current = await inventory.ReadStorageAsync(token);
            await RecordAsync(commitId, plan, current.Revision.Split(':')[^1], token);
            return await inventory.ReadStorageAsync(token);
        }, ct);

    private static string PadJson(string value, int bucket)
    {
        try { using var document = JsonDocument.Parse(value); }
        catch (JsonException) { throw new GenerationStoppedException("storage_commit_plan_invalid"); }
        value = value.TrimEnd(' ', '\t', '\r', '\n');
        int encodedBytes = Encoding.UTF8.GetByteCount(value);
        foreach (char character in value)
            encodedBytes += character is '\\' or '"' or '\b' or '\f' or '\n' or '\r' or '\t' ? 1 : character < 0x20 ? 5 : 0;
        int target = checked(((encodedBytes + bucket - 1) / bucket) * bucket);
        if (target > 131072) throw new GenerationStoppedException("storage_commit_plan_invalid");
        return value + new string(' ', target - encodedBytes);
    }

    private static ManagedStorageCommit Convert(ManagedStorageCommitRecord value)
    {
        static long Number(string text) => text.Length == 20 && text.All(char.IsAsciiDigit) && long.TryParse(text, NumberStyles.None,
            CultureInfo.InvariantCulture, out long number) ? number : throw new GenerationStoppedException("storage_commit_counter_invalid");
        return new(value.CommitId, new(value.BeforeInventoryJson, value.ArtifactCategoriesJson, value.OwnedChangesJson,
            value.RootFingerprint, value.MeasuredAtUtc),
            $"managed-v1:{Number(value.ExpectedCoreRevision)}:{Number(value.ExpectedCoreLogicalBytes)}:{Number(value.ExpectedRevision)}:{Number(value.ExpectedLogicalBytes)}:{value.SchemaFingerprint}",
            Number(value.ExpectedRowCount));
    }
}
