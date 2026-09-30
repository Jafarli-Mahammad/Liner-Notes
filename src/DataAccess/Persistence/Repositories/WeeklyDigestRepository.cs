using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.Domain.Digest;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class WeeklyDigestRepository : IWeeklyDigestRepository
{
    private readonly AppDbContext _context;

    public WeeklyDigestRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<WeeklyDigest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.WeeklyDigests
            .Include(d => d.Recommendations)
                .ThenInclude(r => r.Track)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<WeeklyDigest?> GetByUserIdAndWeekAsync(Guid userId, IsoWeek week, CancellationToken cancellationToken = default)
    {
        return await _context.WeeklyDigests
            .Include(d => d.Recommendations)
                .ThenInclude(r => r.Track)
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Week == week, cancellationToken);
    }

    public async Task<IReadOnlyList<WeeklyDigest>> GetRecentDigestsForUserAsync(Guid userId, int count, CancellationToken cancellationToken = default)
    {
        return await _context.WeeklyDigests
            .Include(d => d.Recommendations)
                .ThenInclude(r => r.Track)
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.WeekStartDate)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsForUserAndWeekAsync(Guid userId, IsoWeek week, CancellationToken cancellationToken = default)
    {
        return await _context.WeeklyDigests
            .AnyAsync(d => d.UserId == userId && d.Week == week, cancellationToken);
    }

    public async Task AddAsync(WeeklyDigest digest, CancellationToken cancellationToken = default)
    {
        await _context.WeeklyDigests.AddAsync(digest, cancellationToken);
    }

    public void Update(WeeklyDigest digest)
    {
        _context.WeeklyDigests.Update(digest);
    }
}
