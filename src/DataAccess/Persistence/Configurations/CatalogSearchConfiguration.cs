using LinerNotes.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class CatalogPublicationConfiguration : IEntityTypeConfiguration<CatalogPublication>
{
    public void Configure(EntityTypeBuilder<CatalogPublication> b)
    {
        b.ToTable("CatalogPublications"); b.HasKey(p => p.Id);
        b.Property(p => p.Source).HasMaxLength(128); b.Property(p => p.SourceVersion).HasMaxLength(128);
        b.Property(p => p.SourceUri).HasMaxLength(1000); b.Property(p => p.Attribution).HasMaxLength(1000);
        b.Property(p => p.MetadataLicense).HasMaxLength(128); b.Property(p => p.TagLicense).HasMaxLength(128);
        b.Property(p => p.NormalizationVersion).HasMaxLength(128); b.Property(p => p.ContentSha256).HasMaxLength(64);
        b.Property(p => p.Origin).HasConversion<string>().HasMaxLength(16);
        b.Property(p => p.State).HasConversion<string>().HasMaxLength(16);
        b.Property(p => p.ImportedRecords).IsConcurrencyToken();
        b.HasIndex(p => new { p.State, p.AcquiredAtUtc });
        b.HasIndex(p => new { p.State, p.ExpiresAtUtc, p.Id });
    }
}
public sealed class CatalogHeadConfiguration : IEntityTypeConfiguration<CatalogHead>
{
    public void Configure(EntityTypeBuilder<CatalogHead> b)
    {
        b.ToTable("CatalogHead"); b.HasKey(h => h.Id); b.Property(h => h.Id).ValueGeneratedNever();
        b.HasOne<CatalogPublication>().WithMany().HasForeignKey(h => h.PublicationId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class CatalogRecordConfiguration : IEntityTypeConfiguration<CatalogRecord>
{
    public void Configure(EntityTypeBuilder<CatalogRecord> b)
    {
        b.ToTable("CatalogRecords", table =>
        {
            table.HasCheckConstraint("CK_CatalogRecords_Identity", "(\"Kind\"='Artist' AND \"ArtistId\" IS NOT NULL AND \"AlbumId\" IS NULL) OR (\"Kind\"='Album' AND \"AlbumId\" IS NOT NULL AND \"ArtistId\" IS NULL) OR (\"Kind\"='Track' AND \"ArtistId\" IS NULL AND \"AlbumId\" IS NULL)");
            table.HasCheckConstraint("CK_CatalogRecords_JsonBound", "octet_length(\"AliasesJson\"::text)<=16384 AND octet_length(\"TagsJson\"::text)<=16384 AND octet_length(\"CreditsJson\"::text)<=16384");
        });
        b.HasKey(r => r.Id); b.HasAlternateKey(r => new { r.Id, r.Kind });
        b.Property(r => r.Kind).HasConversion<string>().HasMaxLength(16);
        b.Property(r => r.DisplayName).HasMaxLength(256); b.Property(r => r.ArtistDisplayName).HasMaxLength(1000);
        b.Property(r => r.Disambiguation).HasMaxLength(500); b.Property(r => r.FirstReleaseDate).HasMaxLength(10);
        b.Property(r => r.RowSha256).HasMaxLength(64).IsRequired();
        b.Property(r => r.ReleasesJson).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
        b.Property(r => r.CreditsJson).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
        b.Property(r => r.AliasesJson).HasColumnType("jsonb"); b.Property(r => r.TagsJson).HasColumnType("jsonb");
        b.HasIndex(r => new { r.PublicationId, r.Kind, r.CanonicalId }).IsUnique();
        b.HasOne<CatalogPublication>().WithMany().HasForeignKey(r => r.PublicationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Artist>().WithMany().HasForeignKey(r => r.ArtistId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Album>().WithMany().HasForeignKey(r => r.AlbumId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class CatalogSearchTermConfiguration : IEntityTypeConfiguration<CatalogSearchTerm>
{
    public void Configure(EntityTypeBuilder<CatalogSearchTerm> b)
    {
        b.ToTable("CatalogSearchTerms"); b.HasKey(t => new { t.RecordId, t.NormalizedTerm });
        b.Property(t => t.NormalizedTerm).HasMaxLength(256).UseCollation("C");
        b.Property(t => t.Kind).HasConversion<string>().HasMaxLength(16);
        b.HasOne<CatalogRecord>().WithMany().HasForeignKey(t => new { t.RecordId, t.Kind })
            .HasPrincipalKey(r => new { r.Id, r.Kind }).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(t => new { t.NormalizedTerm, t.Kind, t.RecordId });
    }
}
public sealed class AlbumCreditConfiguration : IEntityTypeConfiguration<AlbumCredit>
{
    public void Configure(EntityTypeBuilder<AlbumCredit> b)
    {
        b.ToTable("AlbumCredits"); b.HasKey(c => new { c.AlbumId, c.Position });
        b.HasIndex(c => new { c.AlbumId, c.ArtistId }).IsUnique();
        b.HasOne<Album>().WithMany().HasForeignKey(c => c.AlbumId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Artist>().WithMany().HasForeignKey(c => c.ArtistId).OnDelete(DeleteBehavior.Restrict);
    }
}
