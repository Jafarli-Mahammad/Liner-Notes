using LinerNotes.Domain.Email;
using LinerNotes.Domain.Digest;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class EmailSuppressionConfiguration : IEntityTypeConfiguration<EmailSuppression>
{
    public void Configure(EntityTypeBuilder<EmailSuppression> builder)
    {
        builder.ToTable("EmailSuppressions"); builder.HasKey(x => x.UserId);
        builder.Property(x => x.RecipientHash).HasMaxLength(64);
        builder.Property(x => x.Reason).HasMaxLength(100);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
