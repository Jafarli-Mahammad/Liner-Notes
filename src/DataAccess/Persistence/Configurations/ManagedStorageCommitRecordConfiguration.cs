using LinerNotes.DataAccess.Persistence.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class ManagedStorageCommitRecordConfiguration : IEntityTypeConfiguration<ManagedStorageCommitRecord>
{
    public void Configure(EntityTypeBuilder<ManagedStorageCommitRecord> builder)
    {
        builder.ToTable("ManagedStorageCommit");
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.BeforeInventoryJson).HasMaxLength(131072).IsRequired();
        builder.Property(x => x.ArtifactCategoriesJson).HasMaxLength(131072).IsRequired();
        builder.Property(x => x.OwnedChangesJson).HasMaxLength(131072).IsRequired();
        builder.Property(x => x.RootFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SchemaFingerprint).HasMaxLength(32).IsRequired();
        foreach (string property in new[] { nameof(ManagedStorageCommitRecord.ExpectedRevision), nameof(ManagedStorageCommitRecord.ExpectedLogicalBytes),
            nameof(ManagedStorageCommitRecord.ExpectedCoreRevision), nameof(ManagedStorageCommitRecord.ExpectedCoreLogicalBytes), nameof(ManagedStorageCommitRecord.ExpectedRowCount) })
            builder.Property<string>(property).HasMaxLength(20).IsRequired();
    }
}
