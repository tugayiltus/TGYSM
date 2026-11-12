using System.Net;
using Tgysm.Api.IntegrationTests.Infrastructure;

namespace Tgysm.Api.IntegrationTests.Auth;

/// <summary>"Microsoft ile giriş yap" starts the OpenID Connect flow only while Entra is the login method (ADR-0030).</summary>
[Collection(ApiCollection.Name)]
public sealed class EntraStartTests(ApiFactory factory) : IAsyncLifetime
{
    private const string StartPath = "/api/v1/auth/oidc/start";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task The_start_redirects_to_microsoft_with_the_client_and_the_callback()
    {
        await LoginMethods.EnableEntraAsync(factory);

        using var response = await ApiClient.Create(factory).GetAsync(StartPath);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Uri.UnescapeDataString(response.Headers.Location!.ToString());
        Assert.StartsWith($"https://login.microsoftonline.com/{LoginMethods.TenantId}/", location, StringComparison.Ordinal);
        Assert.Contains("client_id=istemci-1", location, StringComparison.Ordinal);
        Assert.Contains($"redirect_uri={ApiFactory.PublicBaseUrl}/api/v1/auth/oidc/callback", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_start_is_not_found_while_another_method_is_active()
    {
        using var response = await ApiClient.Create(factory).GetAsync(StartPath);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
