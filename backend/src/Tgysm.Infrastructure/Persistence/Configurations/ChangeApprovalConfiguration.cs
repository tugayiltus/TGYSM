using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class ChangeApprovalConfiguration : IEntityTypeConfiguration<ChangeApproval>
{
    public void Configure(EntityTypeBuilder<ChangeApproval> builder)
    {
        builder.ToTable("change_approvals", table => table.HasCheckConstraint("ck_change_approvals_status", SnakeCaseEnumConversion<ChangeApprovalStatus>.CheckSql("status")));
        builder.ConfigureEntityBase();
        builder.Property(a => a.Fingerprint).HasMaxLength(ChangeApproval.FingerprintLength).IsRequired();
        builder.Property(a => a.Status).HasEnumName();
        builder.Property(a => a.Reason).HasMaxLength(ChangeApproval.ReasonMaxLength);
        builder.Property(a => a.DecidedById).HasColumnName("decided_by");

        // One open decision per change (ADR-0032).
        builder.HasIndex(a => a.ChangeId).IsUnique().HasFilter("status = 'pending'").HasDatabaseName("ux_change_approvals_pending");
        builder.HasIndex(a => new { a.ChangeId, a.CreatedAt });
        builder.HasOne<ChangeRequest>().WithMany().HasForeignKey(a => a.ChangeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.DecidedById).OnDelete(DeleteBehavior.Restrict);
    }
}
