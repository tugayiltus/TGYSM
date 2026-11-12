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
    public async Task A_user_is_found_by_name_and_only_the_right_password_binds()
    {
        var settings = Settings();
        Assert.SkipWhen(settings is null || Env("USER") is null, "No test directory configured.");

        var match = await Directory().FindAsync(settings!, Env("USER")!, TestContext.Current.CancellationToken);
        var good = await Directory().CheckPasswordAsync(settings!, match.DistinguishedName!, Env("PASSWORD")!, TestContext.Current.CancellationToken);
        var wrong = await Directory().CheckPasswordAsync(settings!, match.DistinguishedName!, "yanlış-şifre", TestContext.Current.CancellationToken);

        Assert.NotNull(match.Identity);
        Assert.Equal((LdapStatus.Ok, LdapStatus.InvalidCredentials), (good, wrong));
    }

    [Fact]
    public async Task A_filter_injection_attempt_finds_no_one()
    {
        var settings = Settings();
        Assert.SkipWhen(settings is null, "No test directory configured.");

        var result = await Directory().FindAsync(settings!, "*)(objectClass=*", TestContext.Current.CancellationToken);

        Assert.Equal(LdapStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task A_directory_that_never_answers_is_unavailable_within_the_time_limit()
    {
        // 10.255.255.1 is a non-routable address: a connection there hangs instead of being refused.
        var settings = new LdapConnectionSettings("10.255.255.1", 636, LdapSecurity.Ldaps, "DC=x", "CN=svc", "x", null);
        var started = System.Diagnostics.Stopwatch.StartNew();

        var match = await Directory().FindAsync(settings, "ahmet", TestContext.Current.CancellationToken);

        Assert.Equal(LdapStatus.Unavailable, match.Status);
        Assert.True(started.Elapsed < NovellLdapDirectory.OperationTimeout + TimeSpan.FromSeconds(5), started.Elapsed.ToString());
    }
}
