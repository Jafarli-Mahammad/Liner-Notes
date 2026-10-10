using LinerNotes.Application.Common.Interfaces;
using LinerNotes.DataAccess.DataContexts;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence.Repositories;

public sealed class UnsubscribeStore(DataContext db) : IUnsubscribeStore
{
    public async Task UnsubscribeAsync(Guid userId, DateTime utcNow, CancellationToken ct)
    {
        if (utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC required.", nameof(utcNow));
        // Atomic row update coordinates with capture's final SELECT FOR UPDATE.
        await db.Users.Where(u => u.Id == userId && u.EmailUnsubscribedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.EmailUnsubscribedAtUtc, utcNow)
                .SetProperty(u => u.LastModifiedAt, utcNow), ct);
    }
}
