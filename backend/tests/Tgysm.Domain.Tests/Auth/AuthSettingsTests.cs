using Tgysm.Domain.Auth;
using Tgysm.Domain.Common;

namespace Tgysm.Domain.Tests.Auth;

public sealed class AuthSettingsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);

    private static AuthSettings WithLdap()
    {
        var settings = AuthSettings.CreateDefault(Now);
        settings.UpdateLdap("dc1.sirket.local", 636, LdapSecurity.Ldaps, "DC=sirket,DC=local", "CN=svc,DC=sirket,DC=local", "protected-1", null, Now);
        return settings;
    }

    [Fact]
    public void The_default_method_is_local() => Assert.Equal(LoginMethod.Local, AuthSettings.CreateDefault(Now).Method);

    [Fact]
    public void A_method_cannot_be_switched_on_before_its_settings_passed_a_test()
    {
        var settings = WithLdap();
        var error = Assert.Throws<DomainException>(() => settings.SwitchTo(LoginMethod.Ldap, Now));
        Assert.Equal(ErrorCodes.AuthMethodNotTested, error.Code);

        settings.MarkTested(LoginMethod.Ldap);
        settings.SwitchTo(LoginMethod.Ldap, Now);
        Assert.Equal(LoginMethod.Ldap, settings.Method);
    }

    [Fact]
    public void Changing_a_setting_after_the_test_needs_a_new_test()
    {
        var settings = WithLdap();
        settings.MarkTested(LoginMethod.Ldap);

        settings.UpdateLdap("dc2.sirket.local", 636, LdapSecurity.Ldaps, "DC=sirket,DC=local", "CN=svc,DC=sirket,DC=local", null, null, Now);

        Assert.False(settings.IsTested(LoginMethod.Ldap));
    }

    [Fact]
    public void Local_needs_no_test() => WithLdap().SwitchTo(LoginMethod.Local, Now);

    [Fact]
    public void A_missing_secret_keeps_the_stored_one_and_domains_are_stored_plain()
    {
        var settings = AuthSettings.CreateDefault(Now);
        settings.UpdateEntra("6f1c2b5e-1a2b-4c3d-8e9f-0a1b2c3d4e5f", "client", "protected-secret", ["@Sirket.com ", "sirket.com.tr"], Now);
        settings.UpdateEntra("6f1c2b5e-1a2b-4c3d-8e9f-0a1b2c3d4e5f", "client", null, ["sirket.com"], Now);

        Assert.Equal("protected-secret", settings.EntraClientSecretProtected);
        Assert.Equal(["sirket.com"], settings.EntraAllowedDomains);
    }

    [Fact]
    public void Allowed_domains_drop_the_at_sign_case_and_spaces()
    {
        var settings = AuthSettings.CreateDefault(Now);
        settings.UpdateEntra("6f1c2b5e-1a2b-4c3d-8e9f-0a1b2c3d4e5f", "client", "s", [" @Sirket.COM", "sirket.com"], Now);
        Assert.Equal(["sirket.com"], settings.EntraAllowedDomains);
    }
}
