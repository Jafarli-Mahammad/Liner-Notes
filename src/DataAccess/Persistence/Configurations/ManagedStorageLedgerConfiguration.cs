using LinerNotes.DataAccess.Persistence.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace LinerNotes.DataAccess.Persistence.Configurations;
public sealed class ManagedStorageLedgerConfiguration : IEntityTypeConfiguration<ManagedStorageLedger>
{
    public void Configure(EntityTypeBuilder<ManagedStorageLedger> builder)
    {
        builder.ToTable("ManagedStorageLedger");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CoreRevision).IsRequired();
        builder.Property(x => x.CoreLogicalBytes).IsRequired();
        builder.Property(x => x.Revision).IsRequired();
        builder.Property(x => x.LogicalBytes).IsRequired();
        builder.Property(x => x.RowCount).IsRequired();
    }
}
