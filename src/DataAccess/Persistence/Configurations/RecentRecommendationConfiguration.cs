using LinerNotes.Domain.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class RecentRecommendationConfiguration : IEntityTypeConfiguration<RecentRecommendation>
{
    public void Configure(EntityTypeBuilder<RecentRecommendation> b)
    {
        b.ToTable("RecentRecommendations");
        b.HasKey(r => r.Sequence); b.Property(r => r.Sequence).ValueGeneratedOnAdd();
        b.HasIndex(r => r.RecordingId).IsUnique();
        b.Property(r => r.Title).HasMaxLength(256);
        b.Property(r => r.ArtistName).HasMaxLength(1000);
        b.Property(r => r.Attribution).HasMaxLength(1000);
        b.Property(r => r.Origin).HasConversion<string>().HasMaxLength(16);
    }
}
