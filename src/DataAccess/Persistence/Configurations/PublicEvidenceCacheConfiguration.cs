using LinerNotes.DataAccess.Persistence.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace LinerNotes.DataAccess.Persistence.Configurations;
public sealed class PublicEvidenceCacheConfiguration : IEntityTypeConfiguration<PublicEvidenceCacheEntry>
{
    public void Configure(EntityTypeBuilder<PublicEvidenceCacheEntry> builder)
    {
        builder.ToTable("PublicEvidenceCache", t => {
            t.HasCheckConstraint("CK_PublicEvidenceCache_Payload", "\"PayloadJson\" IS NULL OR (octet_length(\"PayloadJson\") <= 2097152 AND jsonb_typeof(\"PayloadJson\"::jsonb)='object')");
            t.HasCheckConstraint("CK_PublicEvidenceCache_Origin", "\"Origin\" IS NULL OR \"Origin\" IN ('Live','Synthetic')");
        });
        builder.HasKey(e => e.Key);
        builder.Property(e => e.Key).HasMaxLength(64).UseCollation("C");
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.Provider).HasMaxLength(32);
        builder.Property(e => e.Origin).HasConversion<string>().HasMaxLength(16);
        builder.Property(e => e.Version).HasMaxLength(64);
        // Keep exact serialized bytes for checksum validation; no JSON path queries are needed.
        builder.Property(e => e.PayloadJson).HasColumnType("text");
        builder.Property(e => e.ContentSha256).HasMaxLength(64);
        builder.Property(e => e.StoredBytes).HasComputedColumnSql("COALESCE(octet_length(\"PayloadJson\"),0)", stored: true);
        builder.HasIndex(e => new { e.ExpiresAtUtc, e.Key });
    }
}
