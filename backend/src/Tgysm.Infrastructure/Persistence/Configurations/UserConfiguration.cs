using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    private const string TableName = "users";
    private const string RoleCheckName = "ck_users_role";

    private static readonly string RoleCheckSql =
        $"role IN ('{UserRoleNames.Employee}', '{UserRoleNames.ItStaff}', '{UserRoleNames.Admin}')";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(TableName, table => table.HasCheckConstraint(RoleCheckName, RoleCheckSql));
        builder.ConfigureEntityBase();
        builder.UseApplicationManagedVersion();

        builder.Property(u => u.FirstName).HasMaxLength(User.NameMaxLength).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(User.NameMaxLength).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.Property(u => u.NormalizedEmail).HasMaxLength(User.EmailMaxLength).IsRequired();
        builder.Property(u => u.SearchName).HasMaxLength(User.SearchNameMaxLength).IsRequired();
        builder.Property(u => u.Role).HasConversion(UserRoleConversion.Converter).HasMaxLength(UserRoleConversion.MaxLength);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.SecurityStamp).HasMaxLength(User.SecurityStampMaxLength).IsRequired();

        builder.HasIndex(u => u.NormalizedEmail).IsUnique();

        builder.HasOne<Department>().WithMany().HasForeignKey(u => u.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Location>().WithMany().HasForeignKey(u => u.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
}
