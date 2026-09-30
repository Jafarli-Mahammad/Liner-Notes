using LinerNotes.Application.Common.Interfaces;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Common;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Taste;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.Persistence;

/// <summary>
/// Entity Framework Core database context for Liner Notes persistence.
/// Targets PostgreSQL 16+ via Npgsql and supports ASP.NET Core Data Protection key storage.
/// </summary>
public class AppDbContext : DbContext, IAppDbContext, IDataProtectionKeyContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserMusicConnection> UserMusicConnections => Set<UserMusicConnection>();
    public DbSet<WeeklyDigest> WeeklyDigests => Set<WeeklyDigest>();
    public DbSet<WeeklyRecommendation> WeeklyRecommendations => Set<WeeklyRecommendation>();
    public DbSet<TasteSignal> TasteSignals => Set<TasteSignal>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Album> Albums => Set<Album>();

    // ASP.NET Core Data Protection key storage for secure at-rest token encryption
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

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

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateAuditableEntities();
        return base.SaveChanges();
    }

    private void UpdateAuditableEntities()
    {
        var utcNow = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.LastModifiedAt = utcNow;
            }
        }
    }
}
