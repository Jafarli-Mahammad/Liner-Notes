using System.Linq.Expressions;

namespace LinerNotes.Application.Common.Interfaces.Repositories;

/// <summary>
/// Generic asynchronous repository contract providing standard query and command operations.
/// </summary>
/// <typeparam name="T">Entity class type.</typeparam>
public interface IAsyncRepository<T> where T : class
{
    Task<List<T>> GetAllAsync(
        Expression<Func<T, bool>>? expression = null,
        CancellationToken cancellationToken = default);

    Task<List<T>> GetAllAsync(
        Expression<Func<T, bool>>? expression,
        Func<IQueryable<T>, IQueryable<T>>? include,
        CancellationToken cancellationToken = default);

    Task<T?> GetAsync(
        Expression<Func<T, bool>>? expression = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default);

    Task<T?> FindAsync(
        Expression<Func<T, bool>> expression,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Expression<Func<T, bool>> expression,
        CancellationToken cancellationToken = default);

    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    Task<T> EditAsync(T entity);

    void Remove(T entity);
}
