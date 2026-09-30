using LinerNotes.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class AlbumConfiguration : IEntityTypeConfiguration<Album>
{
    public void Configure(EntityTypeBuilder<Album> builder)
    {
        builder.ToTable("Albums");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.ArtistName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.Mbid)
            .HasMaxLength(64);

        builder.Property(a => a.ArtistId);

        builder.HasIndex(a => a.Mbid);

        builder.HasOne<Artist>()
            .WithMany()
            .HasForeignKey(a => a.ArtistId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
