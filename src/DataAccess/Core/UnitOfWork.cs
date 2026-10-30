using LinerNotes.Application.Common.Interfaces;
using LinerNotes.DataAccess.DataContexts;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Core;

/// <summary>
/// Unit of work orchestrating transactions and change tracking persistence across repositories.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly DataContext _dataContext;

    public UnitOfWork(DataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<T> ExecuteReadSnapshotAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (_dataContext.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Read snapshot requires an independent outer transaction.");
        var strategy = _dataContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dataContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
            await _dataContext.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
            return await operation(cancellationToken);
            // No writes are allowed; disposing the read-only transaction releases its snapshot.
        });
    }

    private async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => await _dataContext.Database.BeginTransactionAsync(cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dataContext.SaveChangesAsync(cancellationToken);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (_dataContext.Database.CurrentTransaction is not null)
            return await operation(cancellationToken);

        var strategy = _dataContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await operation(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                try
                {
                    if (_dataContext.Database.GetDbConnection().State == System.Data.ConnectionState.Open)
                        await transaction.RollbackAsync(CancellationToken.None);
                }
                catch (Exception rollback) when (rollback is System.Data.Common.DbException or IOException or InvalidOperationException)
                {
                    // Preserve the original failure, including an unknown commit outcome.
                    // The durable witness and a fresh scope decide recovery; never claim rollback.
                }
                finally { _dataContext.ChangeTracker.Clear(); }
                throw;
            }
        });
    }
}
