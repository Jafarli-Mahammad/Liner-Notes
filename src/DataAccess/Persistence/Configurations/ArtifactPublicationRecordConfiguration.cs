using LinerNotes.DataAccess.Persistence.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class ArtifactPublicationRecordConfiguration : IEntityTypeConfiguration<ArtifactPublicationRecord>
{
    public void Configure(EntityTypeBuilder<ArtifactPublicationRecord> builder)
    {
        builder.ToTable("ArtifactPublications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Category).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Root).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.FinalName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.StageName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
        // The durable final-name claim prevents duplicate workers, including after publication.
        builder.HasIndex(x => new { x.Root, x.FinalName }).IsUnique().HasFilter("\"Phase\" NOT IN (4,6)");
        builder.HasIndex(x => x.Phase);
        builder.HasIndex(x => new { x.OwnerId, x.Id });
    }
}
