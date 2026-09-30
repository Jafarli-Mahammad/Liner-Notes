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
        builder.HasKey(r => r.Id);

        builder.HasIndex(r => new { r.WeeklyDigestId, r.Rank });

        // Store structured ScoreBreakdown as JSON column for transparent explainability
        builder.Property(r => r.ScoreBreakdown)
            .HasConversion(
                breakdown => JsonSerializer.Serialize(breakdown, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<ScoreBreakdown>(json, (JsonSerializerOptions?)null) ?? new ScoreBreakdown())
            .IsRequired();

        builder.Property(r => r.FeedbackComment)
            .HasMaxLength(1000);
    }
}
