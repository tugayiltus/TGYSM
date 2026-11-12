using Microsoft.Extensions.Logging.Abstractions;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Auth;
using Tgysm.Infrastructure.Identity;

namespace Tgysm.Api.IntegrationTests.Auth;

/// <summary>
/// The real LDAP client against a directory given by environment variables (TGYSM_TEST_LDAP_HOST, _PORT, _SECURITY,
/// _BASE_DN, _BIND_DN, _BIND_PASSWORD, _USER, _PASSWORD). Without them the tests are skipped: CI has no directory (ADR-0030).
/// </summary>
public sealed class NovellLdapDirectoryTests
{
    private const string Prefix = "TGYSM_TEST_LDAP_";

    private static string? Env(string name) => Environment.GetEnvironmentVariable(Prefix + name);

    private static LdapConnectionSettings? Settings() =>
        Env("HOST") is { } host
            ? new LdapConnectionSettings(host, int.Parse(Env("PORT") ?? "636", System.Globalization.CultureInfo.InvariantCulture),
                Enum.Parse<LdapSecurity>(Env("SECURITY") ?? nameof(LdapSecurity.Ldaps), ignoreCase: true), Env("BASE_DN")!, Env("BIND_DN")!, Env("BIND_PASSWORD")!, null)
            : null;

    private static NovellLdapDirectory Directory() => new(NullLogger<NovellLdapDirectory>.Instance);

    [Fact]
    public async Task The_service_account_reaches_the_base_dn()
    {
        var settings = Settings();
        Assert.SkipWhen(settings is null, "No test directory configured.");

        var result = await Directory().TestAsync(settings!, TestContext.Current.CancellationToken);

        Assert.True(result.Ok, result.ErrorCode);
    }

    [Fact]
    public async Task A_user_signs_in_by_name_and_a_wrong_password_fails()
    {
        var settings = Settings();
        Assert.SkipWhen(settings is null || Env("USER") is null, "No test directory configured.");

        var good = await Directory().AuthenticateAsync(settings!, Env("USER")!, Env("PASSWORD")!, TestContext.Current.CancellationToken);
        var wrong = await Directory().AuthenticateAsync(settings!, Env("USER")!, "yanlış-şifre", TestContext.Current.CancellationToken);

        Assert.NotNull(good.Identity);
        Assert.Equal(LdapFailure.InvalidCredentials, wrong.Failure);
    }

    [Fact]
    public async Task A_filter_injection_attempt_finds_no_one()
    {
        var settings = Settings();
        Assert.SkipWhen(settings is null, "No test directory configured.");

        var result = await Directory().AuthenticateAsync(settings!, "*)(objectClass=*", "x", TestContext.Current.CancellationToken);

        Assert.Equal(LdapFailure.InvalidCredentials, result.Failure);
    }
}
