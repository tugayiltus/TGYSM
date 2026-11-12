using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Auth.Directory;
using Tgysm.Domain.Auth;

namespace Tgysm.Api.IntegrationTests.Infrastructure;

/// <summary>An in-memory directory: entries answer to their user name or email (any case), with one password each.</summary>
public sealed class FakeLdapDirectory : ILdapDirectory
{
    private readonly List<(string UserName, string Email, string Password, DirectoryIdentity Identity)> _entries = [];

    public bool IsDown { get; set; }

    public LdapTestResult TestResult { get; set; } = new(true, null);

    public void Reset()
    {
        _entries.Clear();
        IsDown = false;
        TestResult = new(true, null);
    }

    public DirectoryIdentity Add(string userName, string email, string password, string firstName = "Ahmet", string lastName = "Yılmaz")
    {
        var identity = new DirectoryIdentity(LoginMethod.Ldap, Guid.NewGuid().ToString(), email, firstName, lastName);
        _entries.Add((userName, email, password, identity));
        return identity;
    }

    public Task<LdapResult> AuthenticateAsync(LdapConnectionSettings settings, string identifier, string password, CancellationToken cancellationToken)
    {
        if (IsDown)
        {
            return Task.FromResult(LdapResult.Fail(LdapFailure.Unavailable));
        }

        var entry = _entries.FirstOrDefault(e =>
            string.Equals(e.UserName, identifier, StringComparison.OrdinalIgnoreCase) || string.Equals(e.Email, identifier, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(entry.Identity is not null && entry.Password == password ? LdapResult.Success(entry.Identity) : LdapResult.Fail(LdapFailure.InvalidCredentials));
    }

    public Task<LdapTestResult> TestAsync(LdapConnectionSettings settings, CancellationToken cancellationToken) => Task.FromResult(TestResult);
}
