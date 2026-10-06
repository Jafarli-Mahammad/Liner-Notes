using System.Security.Claims;
using LinerNotes.Application.Common.Interfaces;
using LinerNotes.DataAccess.IdentityEntities;
using LinerNotes.Domain.Catalog;
using LinerNotes.Domain.Common;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Taste;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LinerNotes.DataAccess.DataContexts;

/// <summary>
/// Primary Entity Framework Core database context integrating ASP.NET Core Identity,
/// Liner Notes domain aggregates, data protection key storage, and automated audit logging.
/// </summary>
public class DataContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IAppDbContext, IDataProtectionKeyContext
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public new DbSet<User> Users => Set<User>();
    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();
    public DbSet<UserMusicConnection> UserMusicConnections => Set<UserMusicConnection>();
    public DbSet<WeeklyDigest> WeeklyDigests => Set<WeeklyDigest>();
    public DbSet<WeeklyRecommendation> WeeklyRecommendations => Set<WeeklyRecommendation>();
    public DbSet<TasteSignal> TasteSignals => Set<TasteSignal>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DataContext(
        DbContextOptions<DataContext> options,
        IHttpContextAccessor? httpContextAccessor = null) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
        // EF Core Pattern: NoTracking by default for high performance on read-heavy discovery queries
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected DataContext(
        DbContextOptions options,
        IHttpContextAccessor? httpContextAccessor = null) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
        // EF Core Pattern: NoTracking by default for high performance on read-heavy discovery queries
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Place ASP.NET Core Identity authentication tables into dedicated 'identity' schema
        modelBuilder.Entity<ApplicationUser>(b => b.ToTable("Users", "identity"));
        modelBuilder.Entity<IdentityRole<Guid>>(b => b.ToTable("Roles", "identity"));
        modelBuilder.Entity<IdentityUserRole<Guid>>(b => b.ToTable("UserRoles", "identity"));
        modelBuilder.Entity<IdentityUserClaim<Guid>>(b => b.ToTable("UserClaims", "identity"));
        modelBuilder.Entity<IdentityUserLogin<Guid>>(b => b.ToTable("UserLogins", "identity"));
        modelBuilder.Entity<IdentityRoleClaim<Guid>>(b => b.ToTable("RoleClaims", "identity"));
        modelBuilder.Entity<IdentityUserToken<Guid>>(b => b.ToTable("UserTokens", "identity"));

        // Apply all entity configurations in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DataContext).Assembly);

        // Global query filters for soft-deleted entities
        modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
        modelBuilder.Entity<TasteSignal>().HasQueryFilter(s => !s.IsDeleted);
        modelBuilder.Entity<UserMusicConnection>().HasQueryFilter(c => !c.IsDeleted);
        modelBuilder.Entity<WeeklyDigest>().HasQueryFilter(d => !d.IsDeleted);
        modelBuilder.Entity<WeeklyRecommendation>().HasQueryFilter(r => !r.IsDeleted);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ProcessAuditEntries();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ProcessAuditEntries();
        return base.SaveChanges();
    }

    private void ProcessAuditEntries()
    {
        var entries = ChangeTracker.Entries<IAuditableEntity>();
        var userIdClaim = _httpContextAccessor?.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? currentUserId = Guid.TryParse(userIdClaim, out var parsedId) && parsedId != Guid.Empty
            ? parsedId
            : null;
        var utcNow = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedBy ??= currentUserId;
                    entry.Entity.CreatedAt = utcNow;
                    break;

                case EntityState.Modified:
                    entry.Property(m => m.CreatedBy).IsModified = false;
                    entry.Property(m => m.CreatedAt).IsModified = false;
                    entry.Entity.LastModifiedBy = currentUserId;
                    entry.Entity.LastModifiedAt = utcNow;
                    break;

                case EntityState.Deleted:
                    // Soft-delete interceptor
                    entry.State = EntityState.Modified;
                    entry.Property(m => m.CreatedBy).IsModified = false;
                    entry.Property(m => m.CreatedAt).IsModified = false;
                    entry.Entity.LastModifiedBy = currentUserId;
                    entry.Entity.LastModifiedAt = utcNow;
                    entry.Entity.IsDeleted = true;
                    entry.Property(m => m.IsDeleted).IsModified = true;
                    entry.Entity.DeletedBy = currentUserId;
                    entry.Property(m => m.DeletedBy).IsModified = true;
                    entry.Entity.DeletedAt = utcNow;
                    entry.Property(m => m.DeletedAt).IsModified = true;
                    break;
            }
        }
    }
}
