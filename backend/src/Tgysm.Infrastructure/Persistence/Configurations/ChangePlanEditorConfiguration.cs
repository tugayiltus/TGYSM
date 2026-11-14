using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Users;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class ChangePlanEditorConfiguration : IEntityTypeConfiguration<ChangePlanEditor>
{
    public void Configure(EntityTypeBuilder<ChangePlanEditor> builder)
    {
        builder.ToTable("change_plan_editors");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.HasIndex(e => new { e.ChangeId, e.UserId }).IsUnique();
        builder.HasIndex(e => e.UserId);
        builder.HasOne<ChangeRequest>().WithMany().HasForeignKey(e => e.ChangeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
