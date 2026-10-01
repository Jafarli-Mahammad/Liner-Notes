using LinerNotes.Application.Common.Interfaces;
using LinerNotes.DataAccess.DataContexts;
using Microsoft.EntityFrameworkCore.Storage;

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

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => await _dataContext.Database.BeginTransactionAsync(cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dataContext.SaveChangesAsync(cancellationToken);
}