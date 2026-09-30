using LinerNotes.Domain.Digest;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class WeeklyDigestConfiguration : IEntityTypeConfiguration<WeeklyDigest>
{
    public void Configure(EntityTypeBuilder<WeeklyDigest> builder)
    {
        builder.HasKey(d => d.Id);

        // Value object mapping for IsoWeek
        builder.Property(d => d.Week)
            .HasConversion(
                week => week.Value,
                str => IsoWeek.Parse(str))
            .HasMaxLength(16)
            .IsRequired();

        // Enforce idempotent weekly delivery: user cannot receive multiple digests for the same week
        builder.HasIndex(d => new { d.UserId, d.WeekStartDate })
            .IsUnique();

        builder.HasMany(d => d.Recommendations)
            .WithOne()
            .HasForeignKey(r => r.WeeklyDigestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
