using System.Linq.Expressions;
using LinerNotes.Application.Common.Exceptions;
using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.DataAccess.DataContexts;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Core;

/// <summary>
/// Generic asynchronous repository implementation for EF Core entities.
/// Adheres to NoTracking by default and explicit updates for high throughput.
/// </summary>
/// <typeparam name="T">Entity type.</typeparam>
public class AsyncRepository<T> : IAsyncRepository<T> where T : class
{
    protected readonly DataContext DataContext;

    public AsyncRepository(DataContext dataContext)
    {
        DataContext = dataContext;
    }

    public async Task<List<T>> GetAllAsync(
        Expression<Func<T, bool>>? expression = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = DataContext.Set<T>().AsNoTracking();
        if (expression is not null)
            query = query.Where(expression);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<List<T>> GetAllAsync(
        Expression<Func<T, bool>>? expression,
        Func<IQueryable<T>, IQueryable<T>>? include,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = DataContext.Set<T>().AsNoTracking();
        if (expression is not null)
            query = query.Where(expression);
        if (include is not null)
            query = include(query);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<T?> GetAsync(
        Expression<Func<T, bool>>? expression = null,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = DataContext.Set<T>().AsNoTracking();

        if (expression is not null)
            query = query.Where(expression);

        if (include is not null)
            query = include(query);

        var entity = await query.FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            throw new NotFoundException(typeof(T).Name, expression?.ToString() ?? "entity");
        }

        return entity;
    }

    public async Task<T?> FindAsync(
        Expression<Func<T, bool>> expression,
        Func<IQueryable<T>, IQueryable<T>>? include = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = DataContext.Set<T>().AsNoTracking();

        if (include is not null)
            query = include(query);

        return await query.FirstOrDefaultAsync(expression, cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> expression,
        CancellationToken cancellationToken = default)
    {
        return await DataContext.Set<T>().AsNoTracking().AnyAsync(expression, cancellationToken);
    }

    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DataContext.Set<T>().AddAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<T> EditAsync(T entity)
    {
        var keyProperty = DataContext.Model.FindEntityType(typeof(T))?
            .FindPrimaryKey()?
            .Properties
            .FirstOrDefault();

        if (keyProperty is not null)
        {
            var keyValue = keyProperty.GetGetter().GetClrValue(entity);
            var tracked = DataContext.ChangeTracker.Entries<T>()
                .FirstOrDefault(e => Equals(keyProperty.GetGetter().GetClrValue(e.Entity), keyValue));

            if (tracked is not null)
            {
                if (!ReferenceEquals(tracked.Entity, entity))
                {
                    tracked.CurrentValues.SetValues(entity);
                }
                tracked.State = EntityState.Modified;
                return tracked.Entity;
            }
        }

        DataContext.Set<T>().Update(entity);
        await Task.CompletedTask;
        return entity;
    }

    public void Remove(T entity)
    {
        var keyProperty = DataContext.Model.FindEntityType(typeof(T))?
            .FindPrimaryKey()?
            .Properties
            .FirstOrDefault();

        if (keyProperty is not null)
        {
            var keyValue = keyProperty.GetGetter().GetClrValue(entity);
            var tracked = DataContext.ChangeTracker.Entries<T>()
                .FirstOrDefault(e => Equals(keyProperty.GetGetter().GetClrValue(e.Entity), keyValue));

            if (tracked is not null)
            {
                DataContext.Set<T>().Remove(tracked.Entity);
                return;
            }
        }

        DataContext.Set<T>().Remove(entity);
    }
}