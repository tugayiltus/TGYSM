using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class ChangePasswordTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ChangePasswordPath = "/api/v1/auth/change-password";
    private const string Email = "pending@example.com";
    private const string NewPassword = "Brand-new-pass-7";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Pending_user_changes_password_and_old_password_stops_working()
    {
        await TestUsers.CreateAsync(factory, UserRole.Employee, Email, mustChangePassword: true);
        var client = ApiClient.Create(factory);
        await client.LoginAsync(Email);

        using var before = await client.GetAsync(ApiClient.MePath);
        Assert.True((await client.ReadJsonAsync<JsonElement>(before)).GetProperty("mustChangePassword").GetBoolean());

        using var change = await client.PostAsync(ChangePasswordPath, new { currentPassword = TestUsers.DefaultPassword, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        using var after = await client.GetAsync(ApiClient.MePath);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.False((await client.ReadJsonAsync<JsonElement>(after)).GetProperty("mustChangePassword").GetBoolean());

        using var oldLogin = await ApiClient.Create(factory).PostAsync(ApiClient.LoginPath, new { email = Email, password = TestUsers.DefaultPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        await ApiClient.Create(factory).LoginAsync(Email, NewPassword);
    }

    [Fact]
    public async Task Wrong_current_password_is_a_field_error()
    {
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, Email);

        using var response = await client.PostAsync(ChangePasswordPath, new { currentPassword = "Wrong-password-1", newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.CurrentPasswordInvalid, (await ApiClient.ReadFieldCodesAsync(response))["currentPassword"]);
    }

    [Theory]
    [InlineData("short1", ErrorCodes.PasswordPolicy)]
    [InlineData(TestUsers.DefaultPassword, ErrorCodes.PasswordSameAsOld)]
    public async Task Invalid_new_password_is_a_field_error(string newPassword, string expectedCode)
    {
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, Email);

        using var response = await client.PostAsync(ChangePasswordPath, new { currentPassword = TestUsers.DefaultPassword, newPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedCode, (await ApiClient.ReadFieldCodesAsync(response))["newPassword"]);
    }

    [Fact]
    public async Task Changing_password_ends_other_sessions_but_keeps_the_current_one()
    {
        var (current, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, Email);
        var other = ApiClient.Create(factory);
        await other.LoginAsync(Email);

        using var change = await current.PostAsync(ChangePasswordPath, new { currentPassword = TestUsers.DefaultPassword, newPassword = NewPassword });
        using var currentMe = await current.GetAsync(ApiClient.MePath);
        using var otherMe = await other.GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);
        Assert.Equal(HttpStatusCode.OK, currentMe.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, otherMe.StatusCode);
    }
}
