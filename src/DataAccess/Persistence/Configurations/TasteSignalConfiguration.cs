using LinerNotes.Domain.Taste;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class TasteSignalConfiguration : IEntityTypeConfiguration<TasteSignal>
{
    public void Configure(EntityTypeBuilder<TasteSignal> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.TargetValue)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(s => s.NormalizedTargetValue)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(s => s.Context)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasIndex(s => new { s.UserId, s.TargetType, s.NormalizedTargetValue });
    }
}
