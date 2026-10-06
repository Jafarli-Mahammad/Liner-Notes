using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.DataAccess.Core;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Digest;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

/// <summary>
/// Repository managing WeeklyDigest aggregates and recommendation snapshots.
/// </summary>
public sealed class WeeklyDigestRepository : AsyncRepository<WeeklyDigest>, IWeeklyDigestRepository
{
    public WeeklyDigestRepository(DataContext context) : base(context)
    {
    }

    public async Task<WeeklyDigest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DataContext.WeeklyDigests
            .Include(d => d.Recommendations)
                .ThenInclude(r => r.Track)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<WeeklyDigest?> GetByUserIdAndWeekAsync(Guid userId, IsoWeek week, CancellationToken cancellationToken = default)
    {
        return await DataContext.WeeklyDigests
            .Include(d => d.Recommendations)
                .ThenInclude(r => r.Track)
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Week == week, cancellationToken);
    }

    public async Task<IReadOnlyList<WeeklyDigest>> GetRecentDigestsForUserAsync(Guid userId, int count, CancellationToken cancellationToken = default)
    {
        return await DataContext.WeeklyDigests
            .Include(d => d.Recommendations)
                .ThenInclude(r => r.Track)
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.WeekStartDate)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsForUserAndWeekAsync(Guid userId, IsoWeek week, CancellationToken cancellationToken = default)
    {
        return await DataContext.WeeklyDigests
            .AnyAsync(d => d.UserId == userId && d.Week == week, cancellationToken);
    }

    public async Task<WeeklyRecommendation?> GetRecommendationByIdAsync(Guid recommendationId, Guid userId, CancellationToken cancellationToken = default)
    {
        return await DataContext.WeeklyRecommendations
            .Include(r => r.Track)
            .FirstOrDefaultAsync(r => r.Id == recommendationId && r.UserId == userId, cancellationToken);
    }

    public void Update(WeeklyDigest digest)
    {
        DataContext.WeeklyDigests.Update(digest);
    }

    public async Task<WeeklyRecommendation?> GetRecommendationForUpdateAsync(Guid recommendationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (DataContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Feedback replacement requires a transaction.");
        return await DataContext.WeeklyRecommendations
            .FromSqlInterpolated($"SELECT * FROM \"WeeklyRecommendations\" WHERE \"Id\" = {recommendationId} AND \"UserId\" = {userId} FOR UPDATE")
            .Include(r => r.Track)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public void UpdateRecommendation(WeeklyRecommendation recommendation)
    {
        DataContext.Entry(recommendation).State = EntityState.Modified;
    }
}
