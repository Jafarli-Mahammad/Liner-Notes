using LinerNotes.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class TrackConfiguration : IEntityTypeConfiguration<Track>
{
    public void Configure(EntityTypeBuilder<Track> builder)
    {
        builder.ToTable("Tracks");

        builder.HasKey(t => t.Id);

        // Ignore computed property
        builder.Ignore(t => t.TrackKey);

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

        builder.Property(t => t.AlbumTitle)
            .HasMaxLength(256);

        builder.Property(t => t.Mbid)
            .HasMaxLength(64);

        builder.Property(t => t.DurationSeconds);

        builder.Property(t => t.ExternalSpotifyUrl)
            .HasMaxLength(512);

        builder.Property(t => t.ExternalYoutubeUrl)
            .HasMaxLength(512);

        builder.HasIndex(t => t.Mbid);
        builder.HasIndex(t => new { t.NormalizedArtistName, t.NormalizedTitle });
    }
}
