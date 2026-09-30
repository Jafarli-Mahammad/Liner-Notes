using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Taste;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.Application.Common.Interfaces;

/// <summary>
/// Abstraction over the EF Core database context for CQRS handlers and background workers.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<UserMusicConnection> UserMusicConnections { get; }
    DbSet<WeeklyDigest> WeeklyDigests { get; }
    DbSet<WeeklyRecommendation> WeeklyRecommendations { get; }
    DbSet<TasteSignal> TasteSignals { get; }
    DbSet<Track> Tracks { get; }
    DbSet<Artist> Artists { get; }
    DbSet<Album> Albums { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
