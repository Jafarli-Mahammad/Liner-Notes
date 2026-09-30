using LinerNotes.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    public void Configure(EntityTypeBuilder<Track> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(t => t.NormalizedTitle)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(t => t.ArtistName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(t => t.NormalizedArtistName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(t => t.Mbid)
            .HasMaxLength(64);

        builder.HasIndex(t => t.Mbid);
        builder.HasIndex(t => new { t.NormalizedArtistName, t.NormalizedTitle });
    }
}
