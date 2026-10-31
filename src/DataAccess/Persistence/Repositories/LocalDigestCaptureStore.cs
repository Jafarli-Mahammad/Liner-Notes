using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Application.Common.Interfaces.Discovery;
using LinerNotes.Application.Features.Discovery;
using LinerNotes.Application.Common.Interfaces.Recommendation;
using LinerNotes.Application.Common.Mappings;
using LinerNotes.DataAccess.Core;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace LinerNotes.DataAccess.Persistence.Repositories;

/// <summary>Each attempt owns a context without EnableRetryOnFailure. File publication is never replayed by EF.</summary>
public sealed class LocalDigestCaptureStore(string connectionString, DataContext? coordinatedContext = null,
    IDiscoveryWriteCoordinator? coordinator = null, DiscoveryRuntimeOptions? runtime = null) : ILocalDigestCaptureStore
{
    private DataContext Context() => new(new DbContextOptionsBuilder<DataContext>()
        .UseNpgsql(connectionString, o=>o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)).Options);

    private static async Task<(LocalCaptureInputs Inputs, WeeklyDigest? Entity)> LoadAsync(DataContext db, Guid userId, IsoWeek week, bool tracking, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u=>u.Id==userId,ct);
        var query = db.WeeklyDigests.Include(d=>d.Recommendations).ThenInclude(r=>r.Track).Where(d=>d.UserId==userId && d.Week==week);
        var digest = await (tracking ? query.AsTracking() : query.AsNoTracking()).SingleOrDefaultAsync(ct);
        var storage = await new DigestGenerationStore(db,new UnitOfWork(db)).ReadStorageAsync(ct);
        return (new(user?.ToDto(),digest?.ToDto(),storage),digest);
    }

    public async Task<LocalCaptureInputs> ReadAsync(Guid userId, IsoWeek week, CancellationToken ct)
    {
        try { await using var db=Context(); return (await LoadAsync(db,userId,week,false,ct)).Inputs; }
        catch (NpgsqlException ex) when (ex.IsTransient) { throw new LocalCaptureTransientException(); }
        catch (NpgsqlException) { throw new GenerationStoppedException("capture_database_unavailable"); }
    }

    public async Task<ILocalDigestCaptureTransaction> BeginAsync(Guid userId, IsoWeek week, CancellationToken ct)
    {
        var db=Context(); IDbContextTransaction? transaction=null;
        try
        {
            transaction=await db.Database.BeginTransactionAsync(ct);
            // The UPDATE used by unsubscribe waits for this row lock. Deletion waits for account ownership first.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"Users\" WHERE \"Id\"={userId} FOR UPDATE",ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"WeeklyDigests\" WHERE \"UserId\"={userId} AND \"Week\"={week.Value} FOR UPDATE",ct);
            var data=await LoadAsync(db,userId,week,true,ct);
            return new Transaction(db,transaction,data.Inputs,data.Entity);
        }
        catch (Exception ex)
        {
            if(transaction is not null) await transaction.DisposeAsync();await db.DisposeAsync();
            if(ex is NpgsqlException sql) throw sql.IsTransient ? new LocalCaptureTransientException() : new GenerationStoppedException("capture_database_unavailable");
            throw;
        }
    }

    public async Task<LocalCaptureInputs> ReadWithinAdmissionAsync(Guid userId, IsoWeek week, CancellationToken ct)
    {
        var db = coordinatedContext ?? throw new GenerationStoppedException("capture_coordination_missing");
        if (db.Database.CurrentTransaction is null) throw new GenerationStoppedException("capture_requires_admission");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"Users\" WHERE \"Id\"={userId} FOR UPDATE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"WeeklyDigests\" WHERE \"UserId\"={userId} AND \"Week\"={week.Value} FOR UPDATE", ct);
        return (await LoadAsync(db, userId, week, false, ct)).Inputs;
    }

    public async Task MarkCapturedWithinAdmissionAsync(Guid userId, IsoWeek week, CancellationToken ct)
    {
        var db = coordinatedContext ?? throw new GenerationStoppedException("capture_coordination_missing");
        if (db.Database.CurrentTransaction is null) throw new GenerationStoppedException("capture_requires_admission");
        var digest = await db.WeeklyDigests.AsTracking().SingleOrDefaultAsync(d => d.UserId == userId && d.Week == week, ct)
            ?? throw new GenerationStoppedException("digest_not_found");
        await db.Entry(digest).ReloadAsync(ct);
        digest.MarkLocalCaptured();
        await db.SaveChangesAsync(ct);
    }

    public async Task RecordFailureAsync(Guid userId, IsoWeek week, string reason, CancellationToken ct)
    {
        if (reason is not ("email_io_unavailable" or "email_io_forbidden" or "email_alternative_too_large" or "email_message_too_large"))
            throw new ArgumentException("Bounded reason required.",nameof(reason));
        async Task<int> UpdateAsync(DataContext db, CancellationToken token) => await db.WeeklyDigests
            .Where(d => d.UserId == userId && d.Week == week && d.Status != DigestStatus.LocalCaptured && d.Status != DigestStatus.Sent)
            .ExecuteUpdateAsync(update => update.SetProperty(d => d.ErrorMessage, reason).SetProperty(d => d.Status, DigestStatus.Failed), token);
        if (runtime?.ManagedWritesEnabled == true)
        {
            if (coordinator is null || coordinatedContext is null) throw new GenerationStoppedException("capture_coordination_missing");
            await coordinator.ExecuteAsync(65_536, token => UpdateAsync(coordinatedContext, token), ct);
        }
        else
        {
            await using var db = Context();
            await UpdateAsync(db, ct);
        }
    }

    private sealed class Transaction(DataContext db,IDbContextTransaction transaction,LocalCaptureInputs inputs,WeeklyDigest? digest) : ILocalDigestCaptureTransaction
    {
        public LocalCaptureInputs Inputs => inputs;
        public async Task CommitCapturedAsync(CancellationToken ct)
        {
            try
            {
                if(digest is null) throw new GenerationStoppedException("digest_not_found");
                digest.MarkLocalCaptured();await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);
            }
            catch (NpgsqlException ex) when (ex.IsTransient) { throw new LocalCaptureTransientException(); }
            catch (DbUpdateException ex) when (ex.InnerException is NpgsqlException { IsTransient:true }) { throw new LocalCaptureTransientException(); }
            catch (Exception ex) when (ex is NpgsqlException or DbUpdateException)
            { throw new GenerationStoppedException("capture_database_unavailable"); }
        }
        public async ValueTask DisposeAsync() { try { await transaction.DisposeAsync(); } finally { await db.DisposeAsync(); } }
    }
}
