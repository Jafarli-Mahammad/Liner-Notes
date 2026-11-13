using LinerNotes.Domain.Email;
using LinerNotes.Domain.Digest;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace LinerNotes.DataAccess.Persistence.Configurations;

public sealed class EmailDispatchConfiguration : IEntityTypeConfiguration<EmailDispatch>
{
    public void Configure(EntityTypeBuilder<EmailDispatch> builder)
    {
        builder.ToTable("EmailDispatches"); builder.HasKey(x => x.Id);
        builder.Ignore(x => x.Pending);
        builder.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.State).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.RequestKey).HasMaxLength(200);
        builder.Property(x => x.RecipientHash).HasMaxLength(64);
        builder.Property(x => x.Fingerprint).HasMaxLength(64);
        builder.Property(x => x.ProtectedPayload).HasMaxLength(350_000);
        builder.Property(x => x.ProviderMessageId).HasMaxLength(200);
        builder.Property(x => x.Reason).HasMaxLength(100);
        builder.HasIndex(x => x.RequestKey).IsUnique();
        builder.HasIndex(x => x.ProviderMessageId).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Purpose, x.CreatedAt });
        builder.HasIndex(x => new { x.State, x.NextAttemptAt });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
