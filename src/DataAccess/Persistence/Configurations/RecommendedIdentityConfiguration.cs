using LinerNotes.DataAccess.Persistence.Accounting;
using LinerNotes.Domain.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace LinerNotes.DataAccess.Persistence.Configurations;
public sealed class RecommendedIdentityConfiguration : IEntityTypeConfiguration<RecommendedIdentity>
{
    public void Configure(EntityTypeBuilder<RecommendedIdentity> builder)
    {
        builder.ToTable("RecommendedIdentities");
        builder.HasKey(e => new { e.ProfileId, e.Alias });
        builder.Property(e => e.Alias).HasMaxLength(600).UseCollation("C");
        builder.HasOne<ListenerProfile>().WithMany().HasForeignKey(e => e.ProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}
