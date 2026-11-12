using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.Auth;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.Security;

namespace Tgysm.Api.IntegrationTests.Auth;

/// <summary>
/// The step after Microsoft validated the token: the cookie must carry the TGYSM session principal, never Microsoft's,
/// and a refusal must end at the login page (ADR-0030).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class EntraCallbackTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ClaimsPrincipal Token(string tenant) => new(new ClaimsIdentity(
        [new Claim("tid", tenant), new Claim("oid", "oid-1"), new Claim("preferred_username", "ahmet@sirket.com"), new Claim("given_name", "Ahmet"), new Claim("family_name", "Yılmaz")], "entra"));

    private async Task<TicketReceivedContext> ReceiveAsync(ClaimsPrincipal principal)
    {
        var scope = factory.Services.CreateAsyncScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var scheme = new AuthenticationScheme(EntraAuthentication.Scheme, null, typeof(OpenIdConnectHandler));
        var context = new TicketReceivedContext(http, scheme, new OpenIdConnectOptions(), new AuthenticationTicket(principal, EntraAuthentication.Scheme));
        await EntraAuthentication.OnTicketReceivedAsync(context);
        return context;
    }

    [Fact]
    public async Task A_company_sign_in_stores_the_tgysm_session_principal()
    {
        await LoginMethods.EnableEntraAsync(factory);

        var context = await ReceiveAsync(Token(LoginMethods.TenantId));

        Assert.NotNull(context.Principal!.FindFirst(ClaimTypes.NameIdentifier));
        Assert.NotNull(context.Principal.FindFirst(SessionClaims.SecurityStamp));
        Assert.Null(context.Principal.FindFirst("oid"));
        Assert.Equal("/", context.ReturnUri);
    }

    [Fact]
    public async Task A_refused_sign_in_ends_at_the_login_page_with_an_error()
    {
        await LoginMethods.EnableEntraAsync(factory);

        var context = await ReceiveAsync(Token("00000000-0000-0000-0000-000000000009"));

        Assert.True(context.Result?.Handled);
        Assert.Equal(EntraAuthentication.ErrorRedirect, context.Response.Headers.Location.ToString());
    }
}
