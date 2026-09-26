using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Security;

namespace Tgysm.Api.IntegrationTests.Auth;

public sealed class CountingPasswordService : IPasswordService
{
    private readonly PasswordService _inner = new();
    private int _verifyCalls;

    public int VerifyCalls => _verifyCalls;

    public string DummyHash => _inner.DummyHash;

    public string Hash(string password) => _inner.Hash(password);

    public bool Verify(string passwordHash, string password)
    {
        Interlocked.Increment(ref _verifyCalls);
        return _inner.Verify(passwordHash, password);
    }

    public void Reset() => Interlocked.Exchange(ref _verifyCalls, 0);
}

public sealed class CountingPasswordApiFactory : ApiFactory
{
    public CountingPasswordService Passwords { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPasswordService>();
            services.AddSingleton<IPasswordService>(Passwords);
        });
    }
}

/// <summary>Inactive and locked accounts must cost one hash check, like unknown emails (no timing enumeration).</summary>
public sealed class LoginTimingTests(CountingPasswordApiFactory factory) : IClassFixture<CountingPasswordApiFactory>
{
    private const string Email = "inactive@example.com";

    [Fact]
    public async Task Inactive_account_login_runs_one_hash_check()
    {
        await factory.ResetAsync();
        var user = await TestUsers.CreateAsync(factory, UserRole.Employee, Email);
        await using (var db = factory.Database.CreateDbContext())
        {
            var tracked = await db.Users.SingleAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
            tracked.Deactivate(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var client = ApiClient.Create(factory);
        await client.RefreshCsrfAsync();
        factory.Passwords.Reset();

        using var response = await client.PostAsync(ApiClient.LoginPath, new { email = Email, password = "Wrong-password-1" });

        Assert.Equal(1, factory.Passwords.VerifyCalls);
    }
}
