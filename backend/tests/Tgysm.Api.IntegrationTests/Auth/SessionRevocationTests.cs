using System.Net;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class SessionRevocationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Email = "revoked@example.com";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Deactivation_ends_the_session_on_the_next_request()
    {
        var (client, user) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, Email);
        await ChangeUserAsync(user.Id, u => u.Deactivate(DateTimeOffset.UtcNow));

        using var me = await client.GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Role_change_ends_the_session_on_the_next_request()
    {
        var (client, user) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, Email);
        await ChangeUserAsync(user.Id, u => u.ChangeRole(UserRole.ItStaff, DateTimeOffset.UtcNow));

        using var me = await client.GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    private async Task ChangeUserAsync(Guid userId, Action<User> change)
    {
        await using var db = factory.Database.CreateDbContext();
        var user = await db.Users.SingleAsync(u => u.Id == userId, Ct);
        change(user);
        await db.SaveChangesAsync(Ct);
    }
}
