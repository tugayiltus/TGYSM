using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Auth.Directory;

namespace Tgysm.Api.IntegrationTests.Auth;

/// <summary>After Microsoft validated the token: only the configured tenant and allowed domains get in (ADR-0030).</summary>
[Collection(ApiCollection.Name)]
public sealed class EntraSignInTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ClaimsPrincipal Token(string tenant, string email) => new(new ClaimsIdentity(
        [new Claim("tid", tenant), new Claim("oid", "oid-1"), new Claim("email", email), new Claim("given_name", "Ahmet"), new Claim("family_name", "Yılmaz")], "entra"));

    private async Task SignInAsync(ClaimsPrincipal principal)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<EntraSignInHandler>().HandleAsync(principal, Ct);
    }

    private async Task<int> UsersAsync()
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.Users.CountAsync(Ct);
    }

    [Fact]
    public async Task Another_tenant_neither_signs_in_nor_creates_an_account()
    {
        await LoginMethods.EnableEntraAsync(factory);
        var before = await UsersAsync();

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => SignInAsync(Token("00000000-0000-0000-0000-000000000009", "ahmet@sirket.com")));

        Assert.Equal(before, await UsersAsync());
    }

    [Fact]
    public async Task The_company_tenant_signs_in()
    {
        await LoginMethods.EnableEntraAsync(factory);

        await SignInAsync(Token(LoginMethods.TenantId, "ahmet@sirket.com"));

        await using var db = factory.Database.CreateDbContext();
        Assert.True(await db.Users.AnyAsync(u => u.EntraObjectId == "oid-1", Ct));
    }

    [Fact]
    public async Task Nothing_signs_in_through_entra_while_another_method_is_active() =>
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => SignInAsync(Token(LoginMethods.TenantId, "ahmet@sirket.com")));
}
