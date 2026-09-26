using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Tgysm.Api.IntegrationTests.Infrastructure;

namespace Tgysm.Api.IntegrationTests.Auth;

/// <summary>Development runs the API on plain HTTP behind the Vite proxy, which marks requests as HTTPS.</summary>
[Collection(ApiCollection.Name)]
public sealed class DevProxyTests(ApiFactory factory)
{
    private const string ForwardedProtoHeader = "X-Forwarded-Proto";
    private const string Https = "https";

    [Fact]
    public async Task Csrf_token_is_issued_over_http_when_the_dev_proxy_forwards_https()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiClient.CsrfPath);
        request.Headers.Add(ForwardedProtoHeader, Https);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
