using LinerNotes.Domain.Digest;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class UserMusicConnectionConfiguration : IEntityTypeConfiguration<UserMusicConnection>
{
    public void Configure(EntityTypeBuilder<UserMusicConnection> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ExternalUsername)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(c => c.EncryptedToken)
            .HasMaxLength(2048);

        builder.HasIndex(c => new { c.UserId, c.ServiceType })
            .IsUnique();
    }
}
