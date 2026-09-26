using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");
        builder.ConfigureEntityBase();

        builder.Property(d => d.Name).HasMaxLength(Department.NameMaxLength).IsRequired();
        builder.Property(d => d.NormalizedName).HasMaxLength(Department.NameMaxLength).IsRequired();
        builder.HasIndex(d => d.NormalizedName).IsUnique();

        builder.HasOne<User>().WithMany().HasForeignKey(d => d.ApproverUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
