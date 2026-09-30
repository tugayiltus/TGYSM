using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Tgysm.Api.Http;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.Security;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Hardening;

/// <summary>Deferred review findings fixed before Phase 2.</summary>
[Collection(ApiCollection.Name)]
public sealed class HardeningTests(ApiFactory factory) : IAsyncLifetime
{
    private const string NoStore = "no-store";
    private const string TemporaryPasswordDelivery = "temporary_password";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Unknown_api_route_returns_404_problem_not_the_spa()
    {
        using var response = await ApiClient.Create(factory).GetAsync("/api/v1/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.NotFound, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Default_policy_also_requires_a_changed_password()
    {
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await provider.GetDefaultPolicyAsync();

        Assert.Contains(policy.Requirements, r => r is PasswordChangedRequirement);
    }

    [Fact]
    public async Task Login_with_text_that_is_not_an_email_is_rejected_and_not_audited()
    {
        using var response = await ApiClient.Create(factory).PostAsync(ApiClient.LoginPath, new { email = "My-Secret-Pass-1", password = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.InvalidEmail, (await ApiClient.ReadFieldCodesAsync(response))["email"]);
        await using var db = factory.Database.CreateDbContext();
        Assert.False(await db.AuditEvents.AnyAsync(Ct));
    }

    [Fact]
    public async Task Responses_with_a_temporary_password_are_not_cached()
    {
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com");

        using var created = await admin.PostAsync("/api/v1/admin/users", new { firstName = "Ece", lastName = "Nur", email = "ece@example.com", role = UserRoleNames.Employee, delivery = TemporaryPasswordDelivery });
        var userId = (await admin.ReadJsonAsync<JsonElement>(created)).GetProperty("userId").GetGuid();
        using var reset = await admin.PostAsync($"/api/v1/admin/users/{userId}/reset-password", new { delivery = TemporaryPasswordDelivery });

        Assert.Contains(NoStore, created.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(NoStore, reset.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Audit_rows_cannot_be_truncated()
    {
        await using var db = factory.Database.CreateDbContext();

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("TRUNCATE audit_events", Ct));

        Assert.Equal(PostgresErrorCodes.RestrictViolation, error.SqlState);
    }

    [Fact]
    public async Task Cancelled_request_is_not_logged_as_an_error()
    {
        var logger = new RecordingLogger<ApiExceptionHandler>();
        var handler = new ApiExceptionHandler(logger);
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };

        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(aborted.Token), Ct);

        Assert.True(handled);
        Assert.DoesNotContain(logger.Levels, level => level >= LogLevel.Error);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Levels.Add(logLevel);
    }
}
