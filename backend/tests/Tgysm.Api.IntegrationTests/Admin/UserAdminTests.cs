using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class UserAdminTests(ApiFactory factory) : IAsyncLifetime
{
    private const string UsersPath = "/api/v1/admin/users";
    private const string AdminEmail = "admin@example.com";
    private const int MaxPageSize = 100;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task<(ApiClient Client, User User)> LoginAsAdminAsync() =>
        ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, AdminEmail);

    private const string TemporaryPasswordDelivery = "temporary_password";

    private static object NewUserBody(string email, string role = UserRoleNames.Employee, Guid? departmentId = null) =>
        new { firstName = "Deniz", lastName = "Aydın", email, role, departmentId, locationId = (Guid?)null };

    private static async Task<uint> GetVersionAsync(ApiClient admin, Guid userId)
    {
        using var response = await admin.GetAsync($"{UsersPath}/{userId}");
        response.EnsureSuccessStatusCode();
        return (await admin.ReadJsonAsync<JsonElement>(response)).GetProperty("version").GetUInt32();
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.ItStaff)]
    public async Task Non_admin_roles_are_forbidden_including_direct_id_access(UserRole role)
    {
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, role, "user@example.com");
        var other = await TestUsers.CreateAsync(factory, UserRole.Employee, "other@example.com");

        using var list = await client.GetAsync(UsersPath);
        using var byId = await client.GetAsync($"{UsersPath}/{other.Id}");
        using var create = await client.PostAsync(UsersPath, NewUserBody("new@example.com"));

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byId.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(ErrorCodes.Forbidden, await ApiClient.ReadCodeAsync(byId));
    }

    [Fact]
    public async Task Anonymous_request_is_unauthenticated()
    {
        using var response = await ApiClient.Create(factory).GetAsync(UsersPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.Unauthenticated, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Admin_with_pending_password_change_gets_password_change_required()
    {
        await TestUsers.CreateAsync(factory, UserRole.Admin, AdminEmail, mustChangePassword: true);
        var client = ApiClient.Create(factory);
        await client.LoginAsync(AdminEmail);

        using var response = await client.GetAsync(UsersPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.PasswordChangeRequired, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Create_returns_temporary_password_and_new_user_must_change_it()
    {
        var (admin, _) = await LoginAsAdminAsync();

        using var response = await admin.PostAsync(UsersPath, new
        {
            firstName = "Deniz",
            lastName = "Aydın",
            email = "deniz@example.com",
            role = UserRoleNames.Employee,
            delivery = TemporaryPasswordDelivery,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var temporaryPassword = (await admin.ReadJsonAsync<JsonElement>(response)).GetProperty("temporaryPassword").GetString()!;
        var newUser = ApiClient.Create(factory);
        await newUser.LoginAsync("deniz@example.com", temporaryPassword);
        using var me = await newUser.GetAsync(ApiClient.MePath);
        Assert.True((await newUser.ReadJsonAsync<JsonElement>(me)).GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Create_with_email_differing_only_in_case_returns_email_taken()
    {
        var (admin, _) = await LoginAsAdminAsync();
        await TestUsers.CreateAsync(factory, UserRole.Employee, "ali@example.com");

        using var response = await admin.PostAsync(UsersPath, NewUserBody("ALI@Example.com"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.EmailTaken, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Parallel_duplicate_create_creates_one_user()
    {
        const string email = "twice@example.com";
        var (admin, _) = await LoginAsAdminAsync();
        await admin.RefreshCsrfAsync();

        var responses = await Task.WhenAll(admin.PostAsync(UsersPath, NewUserBody(email)), admin.PostAsync(UsersPath, NewUserBody(email)));
        try
        {
            var statuses = responses.Select(r => r.StatusCode).Order().ToArray();
            Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, statuses);
            var conflict = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(ErrorCodes.EmailTaken, await ApiClient.ReadCodeAsync(conflict));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.Users.CountAsync(u => u.NormalizedEmail == email, Ct));
    }

    [Fact]
    public async Task Create_with_inactive_department_returns_reference_inactive()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var department = Department.Create("Muhasebe", null);
        department.Deactivate();
        await using (var db = factory.Database.CreateDbContext())
        {
            db.Departments.Add(department);
            await db.SaveChangesAsync(Ct);
        }

        using var response = await admin.PostAsync(UsersPath, NewUserBody("x@example.com", departmentId: department.Id));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.ReferenceInactive, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Validation_errors_are_reported_per_field()
    {
        var (admin, _) = await LoginAsAdminAsync();

        using var response = await admin.PostAsync(UsersPath, new
        {
            firstName = "",
            lastName = new string('a', User.NameMaxLength + 1),
            email = "not-an-email",
            role = UserRoleNames.Employee,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = await ApiClient.ReadFieldCodesAsync(response);
        Assert.Contains(ErrorCodes.Required, fields["firstName"]);
        Assert.Contains(ErrorCodes.TooLong, fields["lastName"]);
        Assert.Contains(ErrorCodes.InvalidEmail, fields["email"]);
    }

    [Fact]
    public async Task Update_with_stale_version_returns_conflict()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var user = await TestUsers.CreateAsync(factory, UserRole.Employee, "stale@example.com");
        var version = await GetVersionAsync(admin, user.Id);
        object Body(string firstName) => new { version, firstName, lastName = "User", email = "stale@example.com", role = UserRoleNames.Employee };

        using var first = await admin.PutAsync($"{UsersPath}/{user.Id}", Body("Birinci"));
        using var second = await admin.PutAsync($"{UsersPath}/{user.Id}", Body("İkinci"));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(second));
    }

    [Fact]
    public async Task Last_admin_cannot_demote_or_deactivate_self()
    {
        var (admin, self) = await LoginAsAdminAsync();
        var version = await GetVersionAsync(admin, self.Id);

        using var demote = await admin.PutAsync($"{UsersPath}/{self.Id}", new
        {
            version,
            firstName = "Test",
            lastName = "User",
            email = AdminEmail,
            role = UserRoleNames.Employee,
        });
        using var deactivate = await admin.PostAsync($"{UsersPath}/{self.Id}/deactivate", new { version });

        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
        Assert.Equal(ErrorCodes.LastAdmin, await ApiClient.ReadCodeAsync(demote));
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
        Assert.Equal(ErrorCodes.LastAdmin, await ApiClient.ReadCodeAsync(deactivate));

        await TestUsers.CreateAsync(factory, UserRole.Admin, "second-admin@example.com");
        using var allowed = await admin.PutAsync($"{UsersPath}/{self.Id}", new
        {
            version,
            firstName = "Test",
            lastName = "User",
            email = AdminEmail,
            role = UserRoleNames.ItStaff,
        });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Deactivated_user_loses_session_and_reactivated_user_can_log_in_again()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var (userClient, user) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "worker@example.com");

        using var deactivate = await admin.PostAsync($"{UsersPath}/{user.Id}/deactivate", new { version = await GetVersionAsync(admin, user.Id) });
        using var me = await userClient.GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);

        using var reactivate = await admin.PostAsync($"{UsersPath}/{user.Id}/reactivate", new { version = await GetVersionAsync(admin, user.Id) });
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
        await ApiClient.Create(factory).LoginAsync("worker@example.com");
    }

    [Fact]
    public async Task Reset_password_ends_session_and_requires_change()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var (userClient, user) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "forgetful@example.com");

        using var reset = await admin.PostAsync($"{UsersPath}/{user.Id}/reset-password", new { delivery = TemporaryPasswordDelivery });
        var temporaryPassword = (await admin.ReadJsonAsync<JsonElement>(reset)).GetProperty("temporaryPassword").GetString()!;
        using var oldSession = await userClient.GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        var fresh = ApiClient.Create(factory);
        await fresh.LoginAsync("forgetful@example.com", temporaryPassword);
        using var me = await fresh.GetAsync(ApiClient.MePath);
        Assert.True((await fresh.ReadJsonAsync<JsonElement>(me)).GetProperty("mustChangePassword").GetBoolean());
    }

    [Theory]
    [InlineData("ilker ışık")]
    [InlineData("IŞIK")]
    [InlineData("İLKER")]
    public async Task Search_is_turkish_case_insensitive(string search)
    {
        var (admin, _) = await LoginAsAdminAsync();
        await TestUsers.CreateAsync(factory, UserRole.Employee, "ilker@example.com", firstName: "İlker", lastName: "Işık");
        await TestUsers.CreateAsync(factory, UserRole.Employee, "baska@example.com", firstName: "Başka", lastName: "Kişi");

        using var response = await admin.GetAsync($"{UsersPath}?search={Uri.EscapeDataString(search)}");

        var items = (await admin.ReadJsonAsync<JsonElement>(response)).GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("ilker@example.com", items[0].GetProperty("email").GetString());
    }

    [Fact]
    public async Task Page_size_is_capped()
    {
        var (admin, _) = await LoginAsAdminAsync();

        using var response = await admin.GetAsync($"{UsersPath}?pageSize=1000");

        Assert.Equal(MaxPageSize, (await admin.ReadJsonAsync<JsonElement>(response)).GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Unknown_role_filter_is_a_validation_error()
    {
        var (admin, _) = await LoginAsAdminAsync();

        using var response = await admin.GetAsync($"{UsersPath}?role=superuser");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.InvalidValue, (await ApiClient.ReadFieldCodesAsync(response))["role"]);
    }

    [Fact]
    public async Task Create_update_and_role_change_are_audited_with_the_admin_as_actor()
    {
        var (admin, self) = await LoginAsAdminAsync();
        using var created = await admin.PostAsync(UsersPath, NewUserBody("audited@example.com"));
        var userId = (await admin.ReadJsonAsync<JsonElement>(created)).GetProperty("userId").GetGuid();

        using var updated = await admin.PutAsync($"{UsersPath}/{userId}", new
        {
            version = await GetVersionAsync(admin, userId),
            firstName = "Deniz",
            lastName = "Aydın",
            email = "audited@example.com",
            role = UserRoleNames.ItStaff,
        });

        await using var db = factory.Database.CreateDbContext();
        var actions = await db.AuditEvents.Where(a => a.EntityId == userId).Select(a => new { a.Action, a.ActorUserId }).ToListAsync(Ct);
        Assert.Contains(actions, a => a.Action == AuditActions.UserCreated && a.ActorUserId == self.Id);
        Assert.Contains(actions, a => a.Action == AuditActions.UserUpdated && a.ActorUserId == self.Id);
        Assert.Contains(actions, a => a.Action == AuditActions.UserRoleChanged && a.ActorUserId == self.Id);
    }

    [Fact]
    public async Task User_signing_in_and_changing_own_password_does_not_make_admin_edits_stale()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var user = await TestUsers.CreateAsync(factory, UserRole.Employee, "busy@example.com", mustChangePassword: true);
        var version = await GetVersionAsync(admin, user.Id);

        var userClient = ApiClient.Create(factory);
        await userClient.LoginAsync("busy@example.com");
        using var changed = await userClient.PostAsync("/api/v1/auth/change-password", new { currentPassword = TestUsers.DefaultPassword, newPassword = "Busy-new-pass-2026" });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        using var deactivate = await admin.PostAsync($"{UsersPath}/{user.Id}/deactivate", new { version });

        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
    }
}
