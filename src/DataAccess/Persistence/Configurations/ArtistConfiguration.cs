using LinerNotes.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class ArtistConfiguration : IEntityTypeConfiguration<Artist>
{
    public void Configure(EntityTypeBuilder<Artist> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.NormalizedName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.Mbid)
            .HasMaxLength(64);

        builder.HasIndex(a => a.Mbid);
        builder.HasIndex(a => a.NormalizedName);
    }
}
