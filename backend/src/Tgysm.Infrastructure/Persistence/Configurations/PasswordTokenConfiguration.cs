using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class PasswordTokenConfiguration : IEntityTypeConfiguration<PasswordToken>
{
    public void Configure(EntityTypeBuilder<PasswordToken> builder)
    {
        builder.ToTable("password_tokens", table =>
            table.HasCheckConstraint("ck_password_tokens_purpose", SnakeCaseEnumConversion<PasswordTokenPurpose>.CheckSql("purpose")));
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.UserId).IsRequired();
        builder.Property(t => t.Purpose).HasEnumName();
        builder.Property(t => t.TokenHash).HasMaxLength(PasswordToken.TokenHashMaxLength).IsRequired();
        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.UsedAt);
        builder.Property(t => t.CreatedAt).IsRequired();

        // Redeeming looks a token up by its hash; issuing voids the unused tokens of one user and purpose.
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.UserId, t.Purpose });

        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
