using LinerNotes.DataAccess.IdentityEntities;
using LinerNotes.Domain.Digest;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.TimeZone)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(u => u.DeliveryDay)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(u => u.DeliveryHourUtc)
            .IsRequired();

        builder.Property(u => u.NextDigestAt);
        builder.Property(u => u.EmailUnsubscribedAtUtc).HasColumnType("timestamp with time zone");

        // Index on NextDigestAt for fast worker batch polling (NextDigestAt <= UtcNow)
        builder.HasIndex(u => u.NextDigestAt);

        builder.Property(u => u.IsDeleted)
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.CreatedBy);
        builder.Property(u => u.LastModifiedAt);
        builder.Property(u => u.LastModifiedBy);
        builder.Property(u => u.DeletedAt);
        builder.Property(u => u.DeletedBy);

        builder.HasMany(u => u.Connections)
            .WithOne()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.TasteSignals)
            .WithOne()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1:1 Identity link to ApplicationUser
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<User>(u => u.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
