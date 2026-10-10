using LinerNotes.Domain.Digest;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class WeeklyDigestConfiguration : IEntityTypeConfiguration<WeeklyDigest>
{
    public void Configure(EntityTypeBuilder<WeeklyDigest> builder)
    {
        builder.ToTable("WeeklyDigests");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.UserId)
            .IsRequired();

        // Map IsoWeek value object to string representation (e.g. "2026-W39")
        builder.Property(d => d.Week)
            .HasConversion(
                week => week.Value,
                str => IsoWeek.Parse(str))
            .HasMaxLength(16)
            .IsRequired();

        // Ignore computed property
        builder.Ignore(d => d.WeekIdentifier);

        builder.Property(d => d.WeekStartDate)
            .IsRequired();

        builder.Property(d => d.WeekEndDate)
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(d => d.SentAt);

        builder.Property(d => d.ErrorMessage)
            .HasMaxLength(2048);

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.Property(d => d.LastModifiedAt);

        // Enforce one persisted list per user/week; delivery needs a separate receipt.
        builder.HasIndex(d => new { d.UserId, d.WeekStartDate })
            .IsUnique();

        builder.HasIndex(d => new { d.UserId, d.Week });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Recommendations)
            .WithOne()
            .HasForeignKey(r => r.WeeklyDigestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
