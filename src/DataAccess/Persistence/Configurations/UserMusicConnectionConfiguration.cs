using LinerNotes.Domain.Digest;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class UserMusicConnectionConfiguration : IEntityTypeConfiguration<UserMusicConnection>
{
    public void Configure(EntityTypeBuilder<UserMusicConnection> builder)
    {
        builder.ToTable("UserMusicConnections");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId)
            .IsRequired();

        builder.Property(c => c.ServiceType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(c => c.ExternalUsername)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(c => c.EncryptedToken)
            .HasMaxLength(2048);

        builder.Property(c => c.IsActive)
            .IsRequired();

        builder.Property(c => c.LastSyncedAt);

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.LastModifiedAt);

        builder.HasIndex(c => new { c.UserId, c.ServiceType })
            .IsUnique();

        builder.HasOne<User>()
            .WithMany(u => u.Connections)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
