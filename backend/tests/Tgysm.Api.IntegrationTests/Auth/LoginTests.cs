using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class LoginTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Email = "employee@example.com";
    private const string CorrelationHeader = "X-Correlation-Id";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Valid_credentials_start_a_session_and_me_returns_the_user()
    {
        await TestUsers.CreateAsync(factory, UserRole.Employee, Email, firstName: "Ayşe", lastName: "Kaya");
        var client = ApiClient.Create(factory);

        await client.LoginAsync(Email);
        using var me = await client.GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await client.ReadJsonAsync<JsonElement>(me);
        Assert.Equal("Ayşe", body.GetProperty("firstName").GetString());
        Assert.Equal(UserRoleNames.Employee, body.GetProperty("role").GetString());
        Assert.False(body.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_return_the_same_error()
    {
        await TestUsers.CreateAsync(factory, UserRole.Employee, Email);
        var client = ApiClient.Create(factory);

        using var wrongPassword = await client.PostAsync(ApiClient.LoginPath, new { email = Email, password = "Wrong-password-1" });
        using var unknownEmail = await client.PostAsync(ApiClient.LoginPath, new { email = "nobody@example.com", password = "Wrong-password-1" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, await ApiClient.ReadCodeAsync(wrongPassword));
        Assert.Equal(ErrorCodes.InvalidCredentials, await ApiClient.ReadCodeAsync(unknownEmail));
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_for_the_right_password()
    {
        var user = await TestUsers.CreateAsync(factory, UserRole.Employee, Email);
        var client = ApiClient.Create(factory);

        for (var attempt = 0; attempt < User.MaxFailedSignInAttempts; attempt++)
        {
            using var failed = await client.PostAsync(ApiClient.LoginPath, new { email = Email, password = "Wrong-password-1" });
        }

        using var correct = await client.PostAsync(ApiClient.LoginPath, new { email = Email, password = TestUsers.DefaultPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, correct.StatusCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, await ApiClient.ReadCodeAsync(correct));
        await using var db = factory.Database.CreateDbContext();
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.LockedOut && a.EntityId == user.Id, Ct));
    }

    [Fact]
    public async Task Inactive_user_cannot_log_in()
    {
        var user = await TestUsers.CreateAsync(factory, UserRole.Employee, Email);
        await using (var db = factory.Database.CreateDbContext())
        {
            var tracked = await db.Users.SingleAsync(u => u.Id == user.Id, Ct);
            tracked.Deactivate(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(Ct);
        }

        using var response = await ApiClient.Create(factory).PostAsync(ApiClient.LoginPath, new { email = Email, password = TestUsers.DefaultPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_without_csrf_token_is_rejected()
    {
        await TestUsers.CreateAsync(factory, UserRole.Employee, Email);

        using var response = await ApiClient.Create(factory).PostAsync(
            ApiClient.LoginPath, new { email = Email, password = TestUsers.DefaultPassword }, withCsrf: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.CsrfInvalid, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Empty_credentials_return_field_errors()
    {
        using var response = await ApiClient.Create(factory).PostAsync(ApiClient.LoginPath, new { email = "", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = await ApiClient.ReadFieldCodesAsync(response);
        Assert.Contains(ErrorCodes.Required, fields["email"]);
        Assert.Contains(ErrorCodes.Required, fields["password"]);
    }

    [Fact]
    public async Task Me_without_session_returns_unauthenticated()
    {
        using var response = await ApiClient.Create(factory).GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.Unauthenticated, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, Email);

        using var logout = await client.PostAsync(ApiClient.LogoutPath);
        using var me = await client.GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Correlation_id_header_is_echoed_and_stored_in_audit()
    {
        const string correlationId = "e2e-login-42";
        await TestUsers.CreateAsync(factory, UserRole.Employee, Email);
        var client = ApiClient.Create(factory);
        await client.RefreshCsrfAsync();
        client.Http.DefaultRequestHeaders.Add(CorrelationHeader, correlationId);

        using var response = await client.PostAsync(ApiClient.LoginPath, new { email = Email, password = TestUsers.DefaultPassword });

        Assert.Equal(correlationId, response.Headers.GetValues(CorrelationHeader).Single());
        await using var db = factory.Database.CreateDbContext();
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.LoginSucceeded && a.CorrelationId == correlationId, Ct));
    }
}
