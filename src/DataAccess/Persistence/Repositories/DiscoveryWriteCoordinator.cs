using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.DataAccess.DataContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace LinerNotes.DataAccess.Persistence.Repositories;

/// <summary>Serialize only admission and mutation. This scope must never enclose acquisition.</summary>
public sealed class DiscoveryWriteCoordinator(DataContext db, IUnitOfWork unit,
    IDigestGenerationStore inventory, ICoordinatedGenerationStorage storage) : IDiscoveryWriteCoordinator
{
    private bool active;
    private long outerEstimate;
    public async Task<T> ExecuteAsync<T>(long estimatedBytes, Func<CancellationToken, Task<T>> write, CancellationToken ct)
    {
        if (estimatedBytes is < 0 or > 67_108_864) throw new GenerationStoppedException("storage_estimate_invalid");
        if (active)
        {
            if (estimatedBytes > outerEstimate) throw new GenerationStoppedException("nested_storage_reservation_exceeded");
            return await write(ct);
        }
        if (db.Database.CurrentTransaction is not null) throw new GenerationStoppedException("storage_requires_outer_admission");
        await using var databaseLease = await inventory.AcquireBatchAsync(ct);
        var before = await inventory.ReadStorageAsync(ct);
        await using var lease = await storage.AcquireCoordinatedAsync(before, ct);
        var reservation = await lease.ReserveAsync(estimatedBytes, 0, before, ct);
        GenerationStorageState? committed = null;
        active = true; outerEstimate = estimatedBytes;
        T result;
        try
        {
            result = await unit.ExecuteInTransactionAsync(async token =>
            {
                // Reacquire at transaction scope if an execution strategy had to reconnect.
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1280200758)", token);
                long logicalBefore = await ReadLogicalBytesAsync(lockRow: true, token);
                await reservation.ValidateAsync(await inventory.ReadStorageAsync(token), token);
                var value = await write(token);
                var after = await inventory.ReadStorageAsync(token);
                if (before.Revision[(before.Revision.LastIndexOf(':') + 1)..] != after.Revision[(after.Revision.LastIndexOf(':') + 1)..])
                    throw new GenerationStoppedException("managed_storage_schema_changed");
                long logicalAfter = await ReadLogicalBytesAsync(lockRow: false, token);
                await reservation.VerifyStoredAsync(Math.Max(0, logicalAfter - logicalBefore), after, token);
                committed = after;
                return value;
            }, ct);
        }
        finally { active = false; outerEstimate = 0; }
        // A cancelled HTTP caller must not interrupt the bookkeeping after a successful database commit.
        using var publicationDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await lease.PublishCommittedInventoryAsync(committed ?? throw new GenerationStoppedException("storage_commit_unknown"), publicationDeadline.Token);
        return result;
    }
    private async Task<long> ReadLogicalBytesAsync(bool lockRow, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT \"LogicalBytes\" FROM \"ManagedStorageLedger\" WHERE \"Id\"=1" + (lockRow ? " FOR UPDATE" : "");
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
    }
}
