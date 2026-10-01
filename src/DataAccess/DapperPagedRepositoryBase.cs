using System.Data;
using Dapper;
using LinerNotes.Application.Common.Models;
using LinerNotes.DataAccess.DataContexts;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Core;

/// <summary>
/// Abstract base repository leveraging Dapper for high-performance direct SQL queries with pagination.
/// </summary>
/// <typeparam name="TEntity">Underlying entity type.</typeparam>
public abstract class DapperPagedRepositoryBase<TEntity> : AsyncRepository<TEntity> where TEntity : class
{
    protected DapperPagedRepositoryBase(DataContext dataContext) : base(dataContext)
    {
    }

    protected static Guid[] PrepareIds(IReadOnlyCollection<Guid>? values)
    {
        var ids = values?.Where(id => id != Guid.Empty).Distinct().ToArray();
        return ids is { Length: > 0 } ? ids : [Guid.Empty];
    }

    protected static IReadOnlyCollection<Guid> ParseIds(string? values)
    {
        if (string.IsNullOrWhiteSpace(values))
        {
            return Array.Empty<Guid>();
        }

        var span = values.AsSpan();
        var list = new List<Guid>();
        foreach (var range in span.Split(','))
        {
            var segment = span[range].Trim();
            if (!segment.IsEmpty && Guid.TryParse(segment, out var id))
            {
                list.Add(id);
            }
        }

        return list;
    }

    protected async Task<PagedResult<TItemDto>> GetPagedInternalAsync<TItemDto, TRow>(
        int page,
        int pageSize,
        DynamicParameters parameters,
        string countSql,
        string dataSql,
        Func<TRow, TItemDto> mapRow,
        CancellationToken ct = default)
    {
        var actualPage = page < 1 ? 1 : page;
        var actualPageSize = pageSize < 1 ? 10 : pageSize;
        var offset = (actualPage - 1) * actualPageSize;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", actualPageSize);

        var connection = DataContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: ct));

            var rows = (await connection.QueryAsync<TRow>(new CommandDefinition(dataSql, parameters, cancellationToken: ct))).AsList();

            var items = new List<TItemDto>(rows.Count);
            foreach (var row in rows)
                items.Add(mapRow(row));

            return new PagedResult<TItemDto>(items, totalCount, actualPage, actualPageSize);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
