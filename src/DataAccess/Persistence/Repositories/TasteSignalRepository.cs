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
            .Where(s => DataContext.ListenerProfiles.Any(p => p.Id == s.ProfileId && p.UserId == userId))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<TasteSignal> signals, CancellationToken cancellationToken = default)
    {
        var items = signals.ToArray();
        foreach (var group in items.Where(s => s.UserId.HasValue).GroupBy(s => s.UserId!.Value))
        {
            var profileId = await DataContext.ListenerProfiles.Where(p => p.UserId == group.Key)
                .Select(p => p.Id).SingleAsync(cancellationToken);
            foreach (var signal in group) signal.BindRegisteredProfile(group.Key, profileId);
        }
        await DataContext.TasteSignals.AddRangeAsync(items, cancellationToken);
    }

    public async Task<IReadOnlyList<TasteSignal>> GetForExportAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await DataContext.TasteSignals.IgnoreQueryFilters().Where(s => DataContext.ListenerProfiles.Any(p => p.Id == s.ProfileId && p.UserId == userId)).OrderBy(s => s.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);

    public async Task DeleteSignalsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var signals = await DataContext.TasteSignals.Where(s => DataContext.ListenerProfiles.Any(p => p.Id == s.ProfileId && p.UserId == userId)).ToListAsync(cancellationToken);
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
            .Where(s => DataContext.ListenerProfiles.Any(p => p.Id == s.ProfileId && p.UserId == userId) && s.Context.StartsWith(contextPrefix + " - "))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
