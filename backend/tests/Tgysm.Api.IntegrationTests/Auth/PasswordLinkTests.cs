using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Notifications;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Auth;

/// <summary>
/// Account links (spec §4, ADR-0017): admin create/reset with <c>delivery</c>, <c>POST /auth/forgot-password</c> and
/// <c>POST /auth/set-password</c> — single-use, time-limited, superseded by a newer link, no account enumeration.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PasswordLinkTests(ApiFactory factory) : IAsyncLifetime
{
    private const string AdminEmail = "admin@example.com";
    private const string UsersPath = "/api/v1/admin/users";
    private const string ForgotPasswordPath = "/api/v1/auth/forgot-password";
    private const string SetPasswordPath = "/api/v1/auth/set-password";
    private const string NewPassword = "Brand-new-password-2026";
    private const string EmailLink = "email_link";
    private const string TemporaryPassword = "temporary_password";
    private const int DispatchBatch = 50;
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ExpiryTolerance = TimeSpan.FromMinutes(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        factory.EmailSender.Reset();
    }

    public ValueTask DisposeAsync()
    {
        factory.EmailSender.Reset();
        return ValueTask.CompletedTask;
    }

    private Task<(ApiClient Client, User User)> LoginAsAdminAsync() =>
        ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, AdminEmail);

    private static object NewUserBody(string email, string? delivery = null) => delivery is null
        ? new { firstName = "Deniz", lastName = "Kaya", email, role = "employee", departmentId = (Guid?)null, locationId = (Guid?)null }
        : new { firstName = "Deniz", lastName = "Kaya", email, role = "employee", departmentId = (Guid?)null, locationId = (Guid?)null, delivery };

    private static async Task<Guid> CreateUserAsync(ApiClient admin, string email, string? delivery = null)
    {
        using var response = await admin.PostAsync(UsersPath, NewUserBody(email, delivery));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await admin.ReadJsonAsync<JsonElement>(response)).GetProperty("userId").GetGuid();
    }

    private async Task<IReadOnlyList<OutboxEvent>> AccountEventsAsync(Guid userId, string eventType)
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.OutboxEvents.AsNoTracking()
            .Where(e => e.UserId == userId && e.EventType == eventType)
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
            .ToListAsync(Ct);
    }

    /// <summary>The raw token as the email would carry it: the latest event's protected secret, unprotected.</summary>
    private async Task<string> LatestTokenAsync(Guid userId, string eventType)
    {
        var latest = (await AccountEventsAsync(userId, eventType)).Last();
        await using var scope = factory.Services.CreateAsyncScope();
        var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector(OutboxEvent.ProtectedSecretPurpose);
        return protector.Unprotect(latest.ProtectedSecret!);
    }

    /// <summary>One worker dispatcher run: resolves deferred forgot-password requests and queued emails.</summary>
    private async Task DispatchAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DispatchOutboxHandler>().HandleAsync(DispatchBatch, Lease, Ct);
    }

    private static Task<HttpResponseMessage> SetPasswordAsync(ApiClient client, string token, string newPassword = NewPassword) =>
        client.PostAsync(SetPasswordPath, new { token, newPassword });

    private async Task AssertTokenInvalidAsync(string token)
    {
        using var response = await SetPasswordAsync(ApiClient.Create(factory), token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.TokenInvalid, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Create_with_default_email_link_returns_no_password_and_queues_activation_with_expiry()
    {
        var (admin, _) = await LoginAsAdminAsync();

        using var response = await admin.PostAsync(UsersPath, NewUserBody("deniz@example.com"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await admin.ReadJsonAsync<JsonElement>(response);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("temporaryPassword").ValueKind);
        var userId = body.GetProperty("userId").GetGuid();

        var activation = Assert.Single(await AccountEventsAsync(userId, NotificationEventTypes.AccountActivation));
        Assert.NotNull(activation.ProtectedSecret);
        using var payload = JsonDocument.Parse(activation.Payload);
        var expiresAt = DateTimeOffset.Parse(payload.RootElement.GetProperty(OutboxEvent.LinkExpiresAtKey).GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(expiresAt, DateTimeOffset.UtcNow + PasswordToken.ActivationLifetime - ExpiryTolerance, DateTimeOffset.UtcNow + PasswordToken.ActivationLifetime + ExpiryTolerance);

        // The token itself is only in the protected secret; the payload is not secret (ADR-0015).
        var token = await LatestTokenAsync(userId, NotificationEventTypes.AccountActivation);
        Assert.DoesNotContain(token, activation.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain(token, activation.ProtectedSecret!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Activation_email_carries_the_set_password_link_and_its_expiry()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var userId = await CreateUserAsync(admin, "deniz@example.com");
        var token = await LatestTokenAsync(userId, NotificationEventTypes.AccountActivation);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DispatchOutboxHandler>().HandleAsync(50, Lease, Ct);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<SendDueEmailsHandler>().HandleAsync(50, Lease, Ct);
        }

        var message = Assert.Single(factory.EmailSender.Sent);
        Assert.Contains($"{ApiFactory.PublicBaseUrl}/set-password?token={Uri.EscapeDataString(token)}", message.Body, StringComparison.Ordinal);
        await using var db = factory.Database.CreateDbContext();
        var expiresAt = await db.PasswordTokens.Where(t => t.UserId == userId).Select(t => t.ExpiresAt).SingleAsync(Ct);
        await using var clockScope = factory.Services.CreateAsyncScope();
        var formatted = clockScope.ServiceProvider.GetRequiredService<Application.Common.ICompanyClock>().Format(expiresAt);
        Assert.Contains(formatted, message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Set_password_with_a_valid_activation_token_lets_the_user_sign_in_without_a_forced_change()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var userId = await CreateUserAsync(admin, "deniz@example.com");
        var token = await LatestTokenAsync(userId, NotificationEventTypes.AccountActivation);

        using var set = await SetPasswordAsync(ApiClient.Create(factory), token);
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        var user = ApiClient.Create(factory);
        using var login = await user.PostAsync(ApiClient.LoginPath, new { email = "deniz@example.com", password = NewPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.False((await user.ReadJsonAsync<JsonElement>(login)).GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task A_used_token_cannot_be_used_again()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var userId = await CreateUserAsync(admin, "deniz@example.com");
        var token = await LatestTokenAsync(userId, NotificationEventTypes.AccountActivation);
        using var first = await SetPasswordAsync(ApiClient.Create(factory), token);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        await AssertTokenInvalidAsync(token);
    }

    [Fact]
    public async Task An_expired_token_is_rejected()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var userId = await CreateUserAsync(admin, "deniz@example.com");
        var token = await LatestTokenAsync(userId, NotificationEventTypes.AccountActivation);
        await using (var db = factory.Database.CreateDbContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE password_tokens SET expires_at = now() - interval '1 second' WHERE user_id = {userId}", Ct);
        }

        await AssertTokenInvalidAsync(token);
    }

    [Fact]
    public async Task A_token_superseded_by_a_newer_link_is_rejected_and_the_newer_one_works()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var user = await TestUsers.CreateAsync(factory, UserRole.Employee, "forgetful@example.com");
        using (var firstReset = await admin.PostAsync($"{UsersPath}/{user.Id}/reset-password", new { delivery = EmailLink }))
        {
            Assert.Equal(HttpStatusCode.OK, firstReset.StatusCode);
            Assert.Equal(JsonValueKind.Null, (await admin.ReadJsonAsync<JsonElement>(firstReset)).GetProperty("temporaryPassword").ValueKind);
        }

        var oldToken = await LatestTokenAsync(user.Id, NotificationEventTypes.PasswordReset);
        // An empty body means the default delivery, email_link.
        using (var secondReset = await admin.PostAsync($"{UsersPath}/{user.Id}/reset-password", new { }))
        {
            Assert.Equal(HttpStatusCode.OK, secondReset.StatusCode);
        }

        var newToken = await LatestTokenAsync(user.Id, NotificationEventTypes.PasswordReset);
        Assert.NotEqual(oldToken, newToken);

        await AssertTokenInvalidAsync(oldToken);
        using var set = await SetPasswordAsync(ApiClient.Create(factory), newToken);
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
    }

    [Fact]
    public async Task An_unknown_token_is_rejected_with_the_same_code()
    {
        await AssertTokenInvalidAsync("not-a-real-token");
    }

    [Fact]
    public async Task A_weak_new_password_is_a_validation_error_and_leaves_the_token_usable()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var userId = await CreateUserAsync(admin, "deniz@example.com");
        var token = await LatestTokenAsync(userId, NotificationEventTypes.AccountActivation);

        using var weak = await SetPasswordAsync(ApiClient.Create(factory), token, "short");
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Contains(ErrorCodes.PasswordPolicy, (await ApiClient.ReadFieldCodesAsync(weak))["newPassword"]);

        using var strong = await SetPasswordAsync(ApiClient.Create(factory), token);
        Assert.Equal(HttpStatusCode.NoContent, strong.StatusCode);
    }

    [Fact]
    public async Task Parallel_set_password_with_one_token_succeeds_exactly_once()
    {
        const int attempts = 6;
        var (admin, _) = await LoginAsAdminAsync();
        var userId = await CreateUserAsync(admin, "deniz@example.com");
        var token = await LatestTokenAsync(userId, NotificationEventTypes.AccountActivation);
        var clients = new List<ApiClient>();
        for (var i = 0; i < attempts; i++)
        {
            var client = ApiClient.Create(factory);
            await client.RefreshCsrfAsync();
            clients.Add(client);
        }

        var responses = await Task.WhenAll(clients.Select((client, i) => SetPasswordAsync(client, token, $"{NewPassword}-{i}")));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
            Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.NoContent), r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));

            await using var db = factory.Database.CreateDbContext();
            Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.UserPasswordSetByLink, Ct));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task Forgot_password_does_equal_request_work_for_unknown_inactive_and_active_emails()
    {
        var active = await TestUsers.CreateAsync(factory, UserRole.Employee, "active@example.com");
        var inactive = await TestUsers.CreateAsync(factory, UserRole.Employee, "inactive@example.com");
        await using (var db = factory.Database.CreateDbContext())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE users SET is_active = false WHERE id = {inactive.Id}", Ct);
        }

        var emails = new[] { "nobody@example.com", "inactive@example.com", "ACTIVE@example.com" };
        var answers = new List<(HttpStatusCode Status, string Body, string Headers)>();
        foreach (var email in emails)
        {
            var client = ApiClient.Create(factory);
            await client.RefreshCsrfAsync();
            using var response = await client.PostAsync(ForgotPasswordPath, new { email });
            answers.Add((response.StatusCode, await response.Content.ReadAsStringAsync(Ct), StableHeaders(response)));
        }

        Assert.All(answers, a => Assert.Equal(HttpStatusCode.Accepted, a.Status));
        Assert.Single(answers.Select(a => a.Body).Distinct());
        Assert.Single(answers.Select(a => a.Headers).Distinct());

        // The request path never looks the account up: one identical deferred event per request, nothing else.
        await using (var db = factory.Database.CreateDbContext())
        {
            var requested = await db.OutboxEvents.AsNoTracking().Where(e => e.EventType == InternalEventTypes.PasswordResetRequested).ToListAsync(Ct);
            Assert.Equal(emails.Length, requested.Count);
            Assert.All(requested, e => Assert.Null(e.UserId));
            Assert.All(requested, e => Assert.Null(e.ProtectedSecret));
            var requestedEmails = requested.Select(e => JsonDocument.Parse(e.Payload).RootElement.GetProperty(OutboxEvent.RequestedEmailKey).GetString()).Order(StringComparer.Ordinal);
            Assert.Equal(emails.Select(User.NormalizeEmail).Order(StringComparer.Ordinal), requestedEmails);
            Assert.False(await db.PasswordTokens.AnyAsync(Ct));
            Assert.False(await db.OutboxEvents.AnyAsync(e => e.EventType == NotificationEventTypes.PasswordReset, Ct));
            Assert.False(await db.AuditEvents.AnyAsync(Ct));
        }

        await DispatchAsync();

        await using var check = factory.Database.CreateDbContext();
        Assert.Equal(active.Id, await check.PasswordTokens.Select(t => t.UserId).SingleAsync(Ct));
        Assert.Single(await AccountEventsAsync(active.Id, NotificationEventTypes.PasswordReset));
        Assert.Equal(1, await check.OutboxEvents.CountAsync(e => e.EventType == NotificationEventTypes.PasswordReset, Ct));
        var audit = Assert.Single(await check.AuditEvents.AsNoTracking().ToListAsync(Ct));
        Assert.Equal(AuditActions.UserPasswordResetLinkSent, audit.Action);
        Assert.Equal(active.Id, audit.EntityId);
        Assert.Null(audit.ActorUserId);
        var token = await LatestTokenAsync(active.Id, NotificationEventTypes.PasswordReset);
        var auditText = string.Join('|', audit.OldValues, audit.NewValues, audit.Reason);
        Assert.All(emails.Append(token), secret => Assert.DoesNotContain(secret, auditText, StringComparison.OrdinalIgnoreCase));

        var statuses = await check.OutboxEvents.AsNoTracking()
            .Where(e => e.EventType == InternalEventTypes.PasswordResetRequested)
            .Select(e => e.Status)
            .ToListAsync(Ct);
        Assert.Equal(1, statuses.Count(s => s == OutboxStatus.Dispatched));
        Assert.Equal(2, statuses.Count(s => s == OutboxStatus.Skipped));
        Assert.False(await check.EmailDeliveries.AnyAsync(d => d.TemplateType == InternalEventTypes.PasswordResetRequested, Ct));
    }

    /// <summary>Every response header except the per-request ones (date, correlation id).</summary>
    private static string StableHeaders(HttpResponseMessage response) =>
        string.Join('\n', response.Headers.Concat(response.Content.Headers)
            .Where(h => h.Key is not ("Date" or "X-Correlation-Id"))
            .OrderBy(h => h.Key, StringComparer.Ordinal)
            .Select(h => $"{h.Key}: {string.Join(',', h.Value)}"));

    [Fact]
    public async Task Setting_the_password_by_link_ends_the_users_other_sessions()
    {
        var (session, user) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "forgetful@example.com");
        using (var before = await session.GetAsync(ApiClient.MePath))
        {
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        }

        using (var forgot = await ApiClient.Create(factory).PostAsync(ForgotPasswordPath, new { email = "forgetful@example.com" }))
        {
            Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        }

        await DispatchAsync();
        var token = await LatestTokenAsync(user.Id, NotificationEventTypes.PasswordReset);
        using (var set = await SetPasswordAsync(ApiClient.Create(factory), token))
        {
            Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        }

        using var after = await session.GetAsync(ApiClient.MePath);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Temporary_password_delivery_keeps_the_old_behavior_and_queues_no_link()
    {
        var (admin, _) = await LoginAsAdminAsync();

        using var created = await admin.PostAsync(UsersPath, NewUserBody("deniz@example.com", TemporaryPassword));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(created.Headers.CacheControl?.NoStore);
        var body = await admin.ReadJsonAsync<JsonElement>(created);
        var userId = body.GetProperty("userId").GetGuid();
        var temporaryPassword = body.GetProperty("temporaryPassword").GetString()!;

        using var reset = await admin.PostAsync($"{UsersPath}/{userId}/reset-password", new { delivery = TemporaryPassword });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.True(reset.Headers.CacheControl?.NoStore);
        var resetPassword = (await admin.ReadJsonAsync<JsonElement>(reset)).GetProperty("temporaryPassword").GetString()!;
        Assert.NotEqual(temporaryPassword, resetPassword);

        var user = ApiClient.Create(factory);
        using var login = await user.PostAsync(ApiClient.LoginPath, new { email = "deniz@example.com", password = resetPassword });
        Assert.True((await user.ReadJsonAsync<JsonElement>(login)).GetProperty("mustChangePassword").GetBoolean());

        Assert.Empty(await AccountEventsAsync(userId, NotificationEventTypes.AccountActivation));
        Assert.Empty(await AccountEventsAsync(userId, NotificationEventTypes.PasswordReset));
        await using var db = factory.Database.CreateDbContext();
        Assert.False(await db.PasswordTokens.AnyAsync(t => t.UserId == userId, Ct));
    }

    [Fact]
    public async Task Link_flows_are_audited_and_no_audit_row_contains_a_token_or_its_hash()
    {
        var (admin, _) = await LoginAsAdminAsync();
        var userId = await CreateUserAsync(admin, "deniz@example.com");
        var activationToken = await LatestTokenAsync(userId, NotificationEventTypes.AccountActivation);
        using (var set = await SetPasswordAsync(ApiClient.Create(factory), activationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        }

        using (var reset = await admin.PostAsync($"{UsersPath}/{userId}/reset-password", new { delivery = EmailLink }))
        {
            Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        }

        using (var forgot = await ApiClient.Create(factory).PostAsync(ForgotPasswordPath, new { email = "deniz@example.com" }))
        {
            Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);
        }

        await DispatchAsync();
        var resetToken = await LatestTokenAsync(userId, NotificationEventTypes.PasswordReset);

        await using var db = factory.Database.CreateDbContext();
        var rows = await db.AuditEvents.AsNoTracking().Where(a => a.EntityId == userId).ToListAsync(Ct);
        Assert.Single(rows, r => r.Action == AuditActions.UserActivationLinkSent);
        Assert.Equal(2, rows.Count(r => r.Action == AuditActions.UserPasswordResetLinkSent));
        var setByLink = Assert.Single(rows, r => r.Action == AuditActions.UserPasswordSetByLink);
        Assert.Equal(userId, setByLink.ActorUserId);

        var hashes = await db.PasswordTokens.AsNoTracking().Where(t => t.UserId == userId).Select(t => t.TokenHash).ToListAsync(Ct);
        var secrets = hashes.Concat([activationToken, resetToken]).ToList();
        foreach (var row in rows)
        {
            var text = string.Join('|', row.OldValues, row.NewValues, row.Reason);
            Assert.All(secrets, secret => Assert.DoesNotContain(secret, text, StringComparison.Ordinal));
        }

        // The stored hash is not the token itself (ADR-0017: a database read cannot be replayed as a credential).
        Assert.DoesNotContain(activationToken, hashes);
        Assert.DoesNotContain(resetToken, hashes);
    }
}
