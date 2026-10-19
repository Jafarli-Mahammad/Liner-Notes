namespace LinerNotes.Application.Common.Interfaces;

/// <summary>
/// Unit of work interface coordinating multi-repository transactions and atomic persistence.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
