using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tgysm.Domain.Auth;
using Tgysm.Infrastructure.Persistence.Converters;

namespace Tgysm.Infrastructure.Persistence.Configurations;

internal sealed class AuthSettingsConfiguration : IEntityTypeConfiguration<AuthSettings>
{
    public void Configure(EntityTypeBuilder<AuthSettings> builder)
    {
        builder.ToTable("auth_settings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Version).IsRowVersion();
        builder.Property(s => s.Method).HasConversion(SnakeCaseEnumConversion<LoginMethod>.Converter).HasMaxLength(SnakeCaseEnumConversion<LoginMethod>.MaxLength);
        builder.Property(s => s.LdapSecurity).HasConversion(SnakeCaseEnumConversion<LdapSecurity>.Converter).HasMaxLength(SnakeCaseEnumConversion<LdapSecurity>.MaxLength);
        builder.Property(s => s.EntraTenantId).HasMaxLength(AuthSettings.TextMaxLength);
        builder.Property(s => s.EntraClientId).HasMaxLength(AuthSettings.TextMaxLength);
        builder.Property(s => s.LdapHost).HasMaxLength(AuthSettings.HostMaxLength);
        builder.Property(s => s.LdapBaseDn).HasMaxLength(AuthSettings.TextMaxLength);
        builder.Property(s => s.LdapBindDn).HasMaxLength(AuthSettings.TextMaxLength);
        builder.Property(s => s.LdapUserFilter).HasMaxLength(AuthSettings.TextMaxLength);
    }
}
