using LinerNotes.Domain.Digest;

namespace LinerNotes.Application.Common.Interfaces.Repositories;

public interface IWeeklyDigestRepository : IAsyncRepository<WeeklyDigest>
{
    Task<WeeklyDigest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WeeklyDigest?> GetByUserIdAndWeekAsync(Guid userId, IsoWeek week, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WeeklyDigest>> GetRecentDigestsForUserAsync(Guid userId, int count, CancellationToken cancellationToken = default);
    Task<bool> ExistsForUserAndWeekAsync(Guid userId, IsoWeek week, CancellationToken cancellationToken = default);
    void Update(WeeklyDigest digest);
}
