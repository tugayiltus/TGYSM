using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Auth;
using Tgysm.Application.Features.Auth.Directory;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Auth;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Auth;

/// <summary>A verified directory identity becomes a TGYSM user: linked, created or refreshed (ADR-0030).</summary>
[Collection(ApiCollection.Name)]
public sealed class DirectorySignInTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static DirectoryIdentity Entra(string email, string oid = "oid-1", string first = "Ahmet", string last = "Yılmaz") =>
        new(LoginMethod.Entra, oid, email, first, last);

    private async Task<SessionUser> SignInAsync(DirectoryIdentity identity)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DirectorySignInHandler>().HandleAsync(identity, Ct);
    }

    private async Task<User> UserAsync(Guid id)
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == id, Ct);
    }

    [Fact]
    public async Task A_new_identity_becomes_an_employee_without_a_local_password()
    {
        var session = await SignInAsync(Entra("Ahmet.Yilmaz@sirket.com"));

        var user = await UserAsync(session.UserId);
        Assert.Equal((UserRole.Employee, "ahmet.yilmaz@sirket.com", "oid-1"), (user.Role, user.NormalizedEmail, user.EntraObjectId));
        Assert.False(user.HasLocalPassword);
        Assert.Null(user.DepartmentId);
        await using var db = factory.Database.CreateDbContext();
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.UserCreatedFromDirectory && a.EntityId == user.Id, Ct));
    }

    [Fact]
    public async Task An_existing_user_with_the_same_email_is_linked_not_duplicated()
    {
        var existing = await TestUsers.CreateAsync(factory, UserRole.Employee, "ahmet.yilmaz@sirket.com");

        var session = await SignInAsync(Entra("AHMET.YILMAZ@Sirket.com"));

        Assert.Equal(existing.Id, session.UserId);
        Assert.Equal("oid-1", (await UserAsync(existing.Id)).EntraObjectId);
    }

    [Fact]
    public async Task A_linked_admin_keeps_role_and_local_password()
    {
        var admin = await TestUsers.CreateAsync(factory, UserRole.Admin, "yonetici@sirket.com");

        var session = await SignInAsync(Entra("yonetici@sirket.com"));

        var user = await UserAsync(admin.Id);
        Assert.Equal(UserRole.Admin, session.Role);
        Assert.Equal(admin.PasswordHash, user.PasswordHash);
    }

    [Fact]
    public async Task Later_sign_ins_match_by_directory_id_and_refresh_the_email_and_names()
    {
        var first = await SignInAsync(Entra("ahmet@sirket.com"));

        var second = await SignInAsync(Entra("ahmet.yilmaz@sirket.com", first: "Ahmet Can", last: "Yılmaz"));

        Assert.Equal(first.UserId, second.UserId);
        var user = await UserAsync(first.UserId);
        Assert.Equal(("ahmet.yilmaz@sirket.com", "Ahmet Can"), (user.Email, user.FirstName));
    }

    [Fact]
    public async Task A_deactivated_user_is_refused_and_the_attempt_audited()
    {
        var session = await SignInAsync(Entra("ahmet@sirket.com"));
        await using (var db = factory.Database.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(u => u.Id == session.UserId, Ct);
            user.Deactivate(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(Ct);
        }

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => SignInAsync(Entra("ahmet@sirket.com")));

        await using var check = factory.Database.CreateDbContext();
        Assert.True(await check.AuditEvents.AnyAsync(a => a.Action == AuditActions.LoginFailed && a.EntityId == session.UserId, Ct));
    }

    [Fact]
    public async Task A_refreshed_email_that_another_user_holds_is_refused()
    {
        var first = await SignInAsync(Entra("ahmet@sirket.com"));
        await TestUsers.CreateAsync(factory, UserRole.Employee, "mehmet@sirket.com");

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => SignInAsync(Entra("mehmet@sirket.com")));

        Assert.Equal("ahmet@sirket.com", (await UserAsync(first.UserId)).Email);
    }

    [Fact]
    public async Task A_user_linked_to_another_directory_account_is_not_relinked_by_email()
    {
        var first = await SignInAsync(Entra("ahmet@sirket.com", oid: "oid-A"));

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => SignInAsync(Entra("ahmet@sirket.com", oid: "oid-B")));

        Assert.Equal("oid-A", (await UserAsync(first.UserId)).EntraObjectId);
    }

    [Fact]
    public async Task A_refused_sign_in_does_not_link_the_account()
    {
        var existing = await TestUsers.CreateAsync(factory, UserRole.Employee, "ahmet@sirket.com");
        await using (var db = factory.Database.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(u => u.Id == existing.Id, Ct);
            user.Deactivate(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(Ct);
        }

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => SignInAsync(Entra("ahmet@sirket.com")));

        Assert.Null((await UserAsync(existing.Id)).EntraObjectId);
    }

    [Fact]
    public async Task An_ldap_identity_without_a_guid_is_refused()
    {
        await TestUsers.CreateAsync(factory, UserRole.Employee, "ahmet@sirket.com");

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => SignInAsync(new DirectoryIdentity(LoginMethod.Ldap, "not-a-guid", "ahmet@sirket.com", "Ahmet", "Yılmaz")));
    }
}
