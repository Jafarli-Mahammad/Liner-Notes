using LinerNotes.Application.Common.Interfaces.Repositories;
using LinerNotes.DataAccess.Core;
using LinerNotes.DataAccess.DataContexts;
using LinerNotes.Domain.Digest;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

/// <summary>
/// Repository managing User aggregate persistence, queries for scheduled digests, and authentication linkage.
/// </summary>
public sealed class UserRepository : AsyncRepository<User>, IUserRepository
{
    public UserRepository(DataContext context) : base(context)
    {
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DataContext.Users
            .Include(u => u.Connections)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await DataContext.Users
            .Include(u => u.Connections)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetUsersDueForDigestAsync(DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        return await DataContext.Users
            .Include(u => u.Connections)
            .Where(u => u.NextDigestAt != null && u.NextDigestAt <= asOfUtc)
            .ToListAsync(cancellationToken);
    }

    public void Update(User user)
    {
        DataContext.Users.Update(user);
    }

    public async Task<bool> DeleteOwnedDataAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Must run inside the account transaction. Bulk deletes bypass the soft-delete interceptor.
        if (DataContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Account deletion requires a transaction.");
        await DataContext.WeeklyRecommendations.IgnoreQueryFilters().Where(r => r.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await DataContext.WeeklyDigests.IgnoreQueryFilters().Where(d => d.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await DataContext.TasteSignals.IgnoreQueryFilters().Where(s => s.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await DataContext.UserMusicConnections.IgnoreQueryFilters().Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        return await DataContext.Users.IgnoreQueryFilters().Where(u => u.Id == userId).ExecuteDeleteAsync(cancellationToken) == 1;
    }
}
