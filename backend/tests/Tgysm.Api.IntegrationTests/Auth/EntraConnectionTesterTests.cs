using System.Net;
using System.Text;
using Tgysm.Infrastructure.Identity;
using Tgysm.Domain.Common;

namespace Tgysm.Api.IntegrationTests.Auth;

/// <summary>The Entra connection test names the wrong field from Microsoft's answers (recorded responses, no network).</summary>
public sealed class EntraConnectionTesterTests
{
    private const string Tenant = "6f1c2b5e-1a2b-4c3d-8e9f-0a1b2c3d4e5f";
    private const string Metadata = """{"token_endpoint":"https://login.microsoftonline.com/t/oauth2/v2.0/token"}""";

    private sealed class Recorded(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(answer(request));
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static Task<(bool Ok, string? ErrorCode)> TestAsync(HttpStatusCode tokenStatus, string tokenBody, HttpStatusCode metadataStatus = HttpStatusCode.OK) =>
        new EntraConnectionTester(new HttpClient(new Recorded(r => r.Method == HttpMethod.Get ? Json(metadataStatus, Metadata) : Json(tokenStatus, tokenBody))))
            .TestAsync(Tenant, "istemci", "sır", TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_token_means_the_settings_work() =>
        Assert.Equal((true, (string?)null), await TestAsync(HttpStatusCode.OK, """{"access_token":"x"}"""));

    [Fact]
    public async Task An_unknown_tenant_is_named() =>
        Assert.Equal(ErrorCodes.AuthEntraTenantNotFound, (await TestAsync(HttpStatusCode.OK, "{}", HttpStatusCode.BadRequest)).ErrorCode);

    [Fact]
    public async Task A_wrong_secret_is_named() =>
        Assert.Equal(ErrorCodes.AuthEntraSecretInvalid, (await TestAsync(HttpStatusCode.Unauthorized, """{"error_codes":[7000215]}""")).ErrorCode);

    [Fact]
    public async Task An_unknown_client_is_named() =>
        Assert.Equal(ErrorCodes.AuthEntraClientInvalid, (await TestAsync(HttpStatusCode.BadRequest, """{"error_codes":[700016]}""")).ErrorCode);
}
