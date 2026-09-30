using System.Text.Json;
using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class WeeklyRecommendationConfiguration : IEntityTypeConfiguration<WeeklyRecommendation>
{
    public void Configure(EntityTypeBuilder<WeeklyRecommendation> builder)
    {
        builder.ToTable("WeeklyRecommendations");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.WeeklyDigestId)
            .IsRequired();

        builder.Property(r => r.UserId)
            .IsRequired();

        builder.Property(r => r.Rank)
            .IsRequired();

        // Store structured ScoreBreakdown as jsonb column in PostgreSQL for full explainability
        builder.Property(r => r.ScoreBreakdown)
            .HasColumnType("jsonb")
            .HasConversion(
                breakdown => JsonSerializer.Serialize(breakdown, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<ScoreBreakdown>(json, (JsonSerializerOptions?)null) ?? new ScoreBreakdown())
            .IsRequired();

        builder.Property(r => r.Feedback)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(r => r.FeedbackComment)
            .HasMaxLength(1000);

        builder.Property(r => r.FeedbackGivenAt);

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.LastModifiedAt);

        builder.HasIndex(r => new { r.WeeklyDigestId, r.Rank });
        builder.HasIndex(r => r.UserId);

        builder.HasOne(r => r.Track)
            .WithMany()
            .HasForeignKey("TrackId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WeeklyDigest>()
            .WithMany(d => d.Recommendations)
            .HasForeignKey(r => r.WeeklyDigestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
