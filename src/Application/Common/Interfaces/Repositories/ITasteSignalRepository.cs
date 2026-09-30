using LinerNotes.Domain.Taste;

namespace LinerNotes.Application.Common.Interfaces.Repositories;

public interface ITasteSignalRepository
{
    Task<IReadOnlyList<TasteSignal>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(TasteSignal signal, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<TasteSignal> signals, CancellationToken cancellationToken = default);
    Task DeleteSignalsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
