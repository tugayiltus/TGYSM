using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tgysm.Api.Http;
using Tgysm.Api.IntegrationTests.Infrastructure;

namespace Tgysm.Api.IntegrationTests.Hardening;

/// <summary>
/// Behind nginx the API sees the proxy's address and plain HTTP. Forwarded headers are trusted only from the configured
/// proxy networks, so the login rate limit keys on the real client and a client cannot spoof its address (ADR-0024).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ForwardedHeadersTests(ApiFactory factory)
{
    private const string ProxyNetwork = "172.16.0.0/12";
    private const string ProxyAddress = "172.18.0.5";
    private const string OutsideAddress = "198.51.100.9";
    private const string ClientAddress = "203.0.113.7";
    // The antiforgery cookie is Secure, so this endpoint works only when the request is known to be HTTPS.
    private const string CsrfPath = "/api/v1/auth/csrf";

    private static async Task<HttpContext> ThroughMiddlewareAsync(string remoteAddress, string forwardedFor, string forwardedProto)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        context.Request.Scheme = Uri.UriSchemeHttp;
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        context.Request.Headers["X-Forwarded-Proto"] = forwardedProto;
        var options = ProxyTrust.CreateForwardedHeadersOptions(new ProxyTrustOptions { KnownProxyNetworks = [ProxyNetwork] });
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options));
        await middleware.Invoke(context);
        return context;
    }

    [Fact]
    public async Task A_request_from_the_proxy_network_carries_the_client_address_and_https()
    {
        var context = await ThroughMiddlewareAsync(ProxyAddress, ClientAddress, Uri.UriSchemeHttps);

        Assert.Equal(IPAddress.Parse(ClientAddress), context.Connection.RemoteIpAddress);
        Assert.Equal(Uri.UriSchemeHttps, context.Request.Scheme);
    }

    [Fact]
    public async Task A_spoofed_header_from_outside_the_proxy_network_is_ignored()
    {
        var context = await ThroughMiddlewareAsync(OutsideAddress, ClientAddress, Uri.UriSchemeHttps);

        Assert.Equal(IPAddress.Parse(OutsideAddress), context.Connection.RemoteIpAddress);
        Assert.Equal(Uri.UriSchemeHttp, context.Request.Scheme);
    }

    [Fact]
    public void An_invalid_network_is_refused_with_its_value()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ProxyTrust.CreateForwardedHeadersOptions(new ProxyTrustOptions { KnownProxyNetworks = ["not-a-cidr"] }));

        Assert.Contains("not-a-cidr", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task In_production_only_https_forwarded_by_the_proxy_counts_as_https()
    {
        using var production = factory.WithWebHostBuilder(b => b
            .UseEnvironment(Environments.Production)
            .UseSetting("Http:KnownProxyNetworks:0", ProxyNetwork));

        var viaProxy = await production.Server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Get;
            c.Request.Path = CsrfPath;
            c.Connection.RemoteIpAddress = IPAddress.Parse(ProxyAddress);
            c.Request.Headers["X-Forwarded-Proto"] = Uri.UriSchemeHttps;
        }, TestContext.Current.CancellationToken);
        var spoofed = await production.Server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Get;
            c.Request.Path = CsrfPath;
            c.Connection.RemoteIpAddress = IPAddress.Parse(OutsideAddress);
            c.Request.Headers["X-Forwarded-Proto"] = Uri.UriSchemeHttps;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status200OK, viaProxy.Response.StatusCode);
        Assert.NotEqual(StatusCodes.Status200OK, spoofed.Response.StatusCode);
    }
}
