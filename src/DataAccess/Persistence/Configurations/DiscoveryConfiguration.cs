using LinerNotes.Domain.Discovery;
using LinerNotes.Domain.Digest;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class ListenerProfileConfiguration : IEntityTypeConfiguration<ListenerProfile>
{
    public void Configure(EntityTypeBuilder<ListenerProfile> builder)
    {
        builder.ToTable("ListenerProfiles", table => table.HasCheckConstraint("CK_ListenerProfiles_Lifetime",
            "(\"UserId\" IS NULL AND \"ExpiresAtUtc\" IS NOT NULL) OR (\"UserId\" IS NOT NULL AND \"ExpiresAtUtc\" IS NULL)"));
        builder.HasKey(p => p.Id);
        builder.Ignore(p => p.IsGuest);
        builder.Property(p => p.OnboardingJson).HasColumnType("text").HasMaxLength(4096);
        builder.Property(p => p.Revision).IsConcurrencyToken();
        builder.HasIndex(p => p.UserId).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
        builder.HasIndex(p => p.ExpiresAtUtc).HasFilter("\"ExpiresAtUtc\" IS NOT NULL");
        builder.HasOne<User>().WithOne(u => u.Profile).HasForeignKey<ListenerProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DiscoveryBatchConfiguration : IEntityTypeConfiguration<DiscoveryBatch>
{
    public void Configure(EntityTypeBuilder<DiscoveryBatch> builder)
    {
        builder.ToTable("DiscoveryBatches", t => t.HasCheckConstraint("CK_DiscoveryBatches_Coverage", "\"CoverageJson\" IS NULL OR octet_length(\"CoverageJson\"::text)<=32768"));
        builder.Property(b => b.CoverageJson).HasColumnType("jsonb");
        builder.HasKey(b => b.Id);
        builder.HasAlternateKey(b => new { b.Id, b.ProfileId });
        builder.Property(b => b.RequestId).HasMaxLength(80).IsRequired();
        builder.Property(b => b.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(b => b.CatalogVersion).HasMaxLength(128);
        builder.Property(b => b.EvidenceVersion).HasMaxLength(128);
        builder.HasIndex(b => new { b.ProfileId, b.RequestId }).IsUnique();
        builder.HasIndex(b => new { b.ProfileId, b.CreatedAtUtc, b.Id });
        builder.HasOne<ListenerProfile>().WithMany().HasForeignKey(b => b.ProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class DiscoveryJobConfiguration : IEntityTypeConfiguration<DiscoveryJob>
{
    public void Configure(EntityTypeBuilder<DiscoveryJob> builder)
    {
        builder.ToTable("DiscoveryJobs", table =>
        {
            table.HasCheckConstraint("CK_DiscoveryJobs_PayloadBound", "octet_length(\"PayloadJson\"::text) <= 16384");
            table.HasCheckConstraint("CK_DiscoveryJobs_ResultBound", "\"ResultJson\" IS NULL OR octet_length(\"ResultJson\"::text) <= 32768");
        });
        builder.HasKey(j => j.Id);
        builder.Property(j => j.RequestKey).HasMaxLength(80).IsRequired();
        builder.Property(j => j.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(j => j.Kind).HasConversion<string>().HasMaxLength(16);
        builder.Property(j => j.State).HasConversion<string>().HasMaxLength(16);
        builder.Property(j => j.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(j => j.ResultJson).HasColumnType("jsonb");
        builder.Property(j => j.ProgressCode).HasMaxLength(64).IsRequired();
        builder.Property(j => j.ErrorCode).HasMaxLength(64);
        builder.HasIndex(j => new { j.ProfileId, j.RequestKey }).IsUnique();
        builder.HasIndex(j => new { j.State, j.NextAttemptAtUtc, j.Id });
        builder.HasIndex(j => j.ProfileId).IsUnique().HasDatabaseName("IX_DiscoveryJobs_OneUnfinishedGeneration")
            .HasFilter("\"Kind\" = 'Generation' AND \"State\" IN ('Pending','Running','Retryable')");
        builder.HasOne<ListenerProfile>().WithMany().HasForeignKey(j => j.ProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<DiscoveryBatch>().WithMany().HasForeignKey(j => new { j.ResultBatchId, j.ProfileId })
            .HasPrincipalKey(b => new { b.Id, b.ProfileId }).OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class GuestSessionConfiguration : IEntityTypeConfiguration<GuestSession>
{
    public void Configure(EntityTypeBuilder<GuestSession> builder)
    {
        builder.ToTable("GuestSessions");
        builder.HasKey(s => s.ProfileId);
        builder.Property(s => s.CredentialHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(s => s.CredentialHash).IsUnique();
        builder.HasOne<ListenerProfile>().WithOne().HasForeignKey<GuestSession>(s => s.ProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}
