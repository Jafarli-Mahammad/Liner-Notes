using LinerNotes.Domain.Digest;
using LinerNotes.Domain.Taste;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class TasteSignalConfiguration : IEntityTypeConfiguration<TasteSignal>
{
    public void Configure(EntityTypeBuilder<TasteSignal> builder)
    {
        builder.ToTable("TasteSignals");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.UserId)
            .IsRequired();

        builder.Property(s => s.TargetType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(s => s.TargetValue)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(s => s.NormalizedTargetValue)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(s => s.Weight)
            .IsRequired();

        builder.Property(s => s.Source)
            .HasConversion<string>()
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(s => s.Context)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.LastModifiedAt);

        builder.HasIndex(s => new { s.UserId, s.TargetType, s.NormalizedTargetValue });

        builder.HasOne<User>()
            .WithMany(u => u.TasteSignals)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
