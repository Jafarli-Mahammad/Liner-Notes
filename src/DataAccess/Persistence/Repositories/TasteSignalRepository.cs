using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.DataAccess.Core;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Taste;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

/// <summary>
/// Repository managing User taste signals, provenance records, and batch seeding.
/// </summary>
public sealed class TasteSignalRepository : AsyncRepository<TasteSignal>, ITasteSignalRepository
{
    public TasteSignalRepository(DataContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<TasteSignal>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await DataContext.TasteSignals
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<TasteSignal> signals, CancellationToken cancellationToken = default)
    {
        await DataContext.TasteSignals.AddRangeAsync(signals, cancellationToken);
    }

    public async Task<IReadOnlyList<TasteSignal>> GetForExportAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await DataContext.TasteSignals.IgnoreQueryFilters().Where(s => s.UserId == userId).OrderBy(s => s.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);

    public async Task DeleteSignalsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var signals = await DataContext.TasteSignals.Where(s => s.UserId == userId).ToListAsync(cancellationToken);
        foreach (var signal in signals)
        {
            Remove(signal);
        }
    }

    public async Task DeleteSignalsByContextPrefixAsync(Guid userId, string contextPrefix, CancellationToken cancellationToken = default)
    {
        if (DataContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Feedback signal replacement requires a transaction.");
        await DataContext.TasteSignals.IgnoreQueryFilters()
            .Where(s => s.UserId == userId && s.Context.StartsWith(contextPrefix + " - "))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
