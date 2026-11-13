using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Users;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class ChangeApprovalEditorConfiguration : IEntityTypeConfiguration<ChangeApprovalEditor>
{
    public void Configure(EntityTypeBuilder<ChangeApprovalEditor> builder)
    {
        builder.ToTable("change_approval_editors");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.HasIndex(e => new { e.ApprovalId, e.UserId }).IsUnique();
        builder.HasIndex(e => e.UserId);
        builder.HasOne<ChangeApproval>().WithMany().HasForeignKey(e => e.ApprovalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
