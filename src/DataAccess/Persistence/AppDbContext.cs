using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Taste;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence;

/// <summary>
/// Entity Framework Core database context for Liner Notes persistence.
/// </summary>
public class AppDbContext : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserMusicConnection> UserMusicConnections => Set<UserMusicConnection>();
    public DbSet<WeeklyDigest> WeeklyDigests => Set<WeeklyDigest>();
    public DbSet<WeeklyRecommendation> WeeklyRecommendations => Set<WeeklyRecommendation>();
    public DbSet<TasteSignal> TasteSignals => Set<TasteSignal>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Album> Albums => Set<Album>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration configurations in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global query filter for soft-deleted users
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
    }
}
