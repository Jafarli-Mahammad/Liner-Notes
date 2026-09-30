using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Domain.Taste;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class TasteSignalRepository : ITasteSignalRepository
{
    private readonly AppDbContext _context;

    public TasteSignalRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TasteSignal>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.TasteSignals
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TasteSignal signal, CancellationToken cancellationToken = default)
    {
        await _context.TasteSignals.AddAsync(signal, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<TasteSignal> signals, CancellationToken cancellationToken = default)
    {
        await _context.TasteSignals.AddRangeAsync(signals, cancellationToken);
    }

    public async Task DeleteSignalsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var signals = await _context.TasteSignals.Where(s => s.UserId == userId).ToListAsync(cancellationToken);
        _context.TasteSignals.RemoveRange(signals);
    }
}
