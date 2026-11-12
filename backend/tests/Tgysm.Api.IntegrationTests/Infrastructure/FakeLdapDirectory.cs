using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Auth.Directory;
using Tgysm.Domain.Auth;

namespace Tgysm.Api.IntegrationTests.Infrastructure;

/// <summary>An in-memory directory: entries answer to their user name or email (any case), with one password each.</summary>
public sealed class FakeLdapDirectory : ILdapDirectory
{
    private readonly List<(string UserName, string Email, string Password, DirectoryIdentity Identity)> _entries = [];
    private int _bindAttempts;

    public bool IsDown { get; set; }

    public LdapTestResult TestResult { get; set; } = new(true, null);

    /// <summary>Password checks that reached the directory; a throttle must keep this at or under its limit.</summary>
    public int BindAttempts => _bindAttempts;

    public void Reset()
    {
        _entries.Clear();
        IsDown = false;
        TestResult = new(true, null);
        _bindAttempts = 0;
    }

    public DirectoryIdentity Add(string userName, string email, string password, string firstName = "Ahmet", string lastName = "Yılmaz")
    {
        var identity = new DirectoryIdentity(LoginMethod.Ldap, Guid.NewGuid().ToString(), email, firstName, lastName);
        _entries.Add((userName, email, password, identity));
        return identity;
    }

    public Task<LdapEntryMatch> FindAsync(LdapConnectionSettings settings, string identifier, CancellationToken cancellationToken)
    {
        if (IsDown)
        {
            return Task.FromResult(new LdapEntryMatch(LdapStatus.Unavailable, null, null));
        }

        var entry = _entries.FirstOrDefault(e =>
            string.Equals(e.UserName, identifier, StringComparison.OrdinalIgnoreCase) || string.Equals(e.Email, identifier, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(entry.Identity is null
            ? new LdapEntryMatch(LdapStatus.NotFound, null, null)
            : new LdapEntryMatch(LdapStatus.Ok, "CN=" + entry.UserName, entry.Identity));
    }

    public Task<LdapStatus> CheckPasswordAsync(LdapConnectionSettings settings, string distinguishedName, string password, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _bindAttempts);
        var entry = _entries.FirstOrDefault(e => "CN=" + e.UserName == distinguishedName);
        return Task.FromResult(entry.Password == password ? LdapStatus.Ok : LdapStatus.InvalidCredentials);
    }

    public Task<LdapTestResult> TestAsync(LdapConnectionSettings settings, CancellationToken cancellationToken) => Task.FromResult(TestResult);
}
