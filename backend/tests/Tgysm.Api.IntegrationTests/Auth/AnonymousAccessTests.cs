using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;

namespace Tgysm.Api.IntegrationTests.Auth;

/// <summary>
/// Acceptance criterion 1: an anonymous visitor cannot open an account. The only endpoints that skip the signed-in fallback
/// policy are sign-in, password links, the CSRF token, health and the SPA/404 fallbacks; none of them creates a user.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AnonymousAccessTests(ApiFactory factory) : IAsyncLifetime
{
    private const string UsersPath = "/api/v1/admin/users";
    private const string AnyMethod = "*";

    /// <summary>Every anonymous endpoint as "METHOD pattern"; a new one must be added here on purpose.</summary>
    private static readonly string[] AnonymousEndpoints =
    [
        "GET api/v1/auth/csrf",
        "POST api/v1/auth/login",
        "POST api/v1/auth/forgot-password",
        "POST api/v1/auth/set-password",
        "GET api/v1/health",
        "* api/{**path}",
        "GET,HEAD {*path:nonfile}",
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public void Only_sign_in_password_link_csrf_health_and_fallback_endpoints_allow_anonymous_callers()
    {
        var anonymous = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => $"{string.Join(',', e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [AnyMethod])} {e.RoutePattern.RawText?.TrimStart('/')}")
            .Order(StringComparer.Ordinal);

        // Compared as one text so a failure prints the whole list.
        Assert.Equal(string.Join(Environment.NewLine, AnonymousEndpoints.Order(StringComparer.Ordinal)), string.Join(Environment.NewLine, anonymous));
    }

    [Fact]
    public async Task Anonymous_user_creation_is_unauthenticated_and_creates_nobody()
    {
        var anonymous = ApiClient.Create(factory);

        using var response = await anonymous.PostAsync(UsersPath, new { firstName = "Deniz", lastName = "Aydın", email = "anon@example.com", role = "admin" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.Unauthenticated, await ApiClient.ReadCodeAsync(response));
        await using var db = factory.Database.CreateDbContext();
        Assert.False(await db.Users.AnyAsync(Ct));
    }
}
