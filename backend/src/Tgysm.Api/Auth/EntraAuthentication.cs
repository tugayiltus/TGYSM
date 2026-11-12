using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Tgysm.Api.Security;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Auth.Directory;
using Tgysm.Application.Features.Auth.Settings;
using Tgysm.Infrastructure.Identity;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.Auth;

/// <summary>
/// Microsoft Entra ID over OpenID Connect with options read from the login settings row (ADR-0030). The options are
/// cached by ASP.NET Core; <see cref="EntraOptionsCache"/> drops them after a settings change. While Entra is not
/// configured, placeholder values keep the handler valid; the start endpoint refuses before any challenge.
/// </summary>
internal static class EntraAuthentication
{
    public const string Scheme = "entra";
    public const string ErrorRedirect = "/login?error=sso";
    private const string AfterSignIn = "/";
    private const string AuthorityFormat = "{0}/{1}/v2.0";
    private const string UnconfiguredTenant = "common";
    private const string UnconfiguredClient = "not-configured";
    private static readonly string[] Scopes = ["openid", "profile", "email"];

    public static AuthenticationBuilder AddEntra(this AuthenticationBuilder builder)
    {
        builder.Services.AddSingleton<IConfigureOptions<OpenIdConnectOptions>, EntraOptionsSetup>();
        // Registered after AddInfrastructure, so it replaces the Worker's no-op cache in the API.
        builder.Services.AddSingleton<ILoginProviderCache, EntraOptionsCache>();
        return builder.AddOpenIdConnect(Scheme, _ => { });
    }

    private sealed class EntraOptionsSetup(IServiceScopeFactory scopes) : IConfigureNamedOptions<OpenIdConnectOptions>
    {
        public void Configure(OpenIdConnectOptions options) => Configure(Options.DefaultName, options);

        public void Configure(string? name, OpenIdConnectOptions options)
        {
            if (name != Scheme)
            {
                return;
            }

            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var secrets = scope.ServiceProvider.GetRequiredService<IAuthSecretProtector>();
            // Options are built synchronously by ASP.NET Core; this runs once per cache fill, not per request.
            var settings = db.AuthSettings.AsNoTracking().Single();
            var configured = settings.EntraTenantId.Length > 0 && settings.EntraClientId.Length > 0 && settings.EntraClientSecretProtected is not null;
            options.Authority = string.Format(System.Globalization.CultureInfo.InvariantCulture, AuthorityFormat, EntraConnectionTester.Authority,
                configured ? settings.EntraTenantId : UnconfiguredTenant);
            options.ClientId = configured ? settings.EntraClientId : UnconfiguredClient;
            options.ClientSecret = configured ? secrets.Unprotect(settings.EntraClientSecretProtected!) : null;
            options.CallbackPath = AuthSettingsMapping.OidcCallbackPath;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.UsePkce = true;
            options.MapInboundClaims = false;
            options.SaveTokens = false;
            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.Scope.Clear();
            foreach (var scopeName in Scopes)
            {
                options.Scope.Add(scopeName);
            }

            // The redirect URI registered in Entra is the company address, not whatever host the request came through.
            var redirectUri = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<AppOptions>>().CurrentValue.PublicBaseUrl.TrimEnd('/') + AuthSettingsMapping.OidcCallbackPath;
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                // The handler stores this value after the event and sends it again when it redeems the code.
                context.ProtocolMessage.RedirectUri = redirectUri;
                return Task.CompletedTask;
            };
            options.Events.OnTicketReceived = OnTicketReceivedAsync;
            options.Events.OnRemoteFailure = context =>
            {
                context.Response.Redirect(ErrorRedirect);
                context.HandleResponse();
                return Task.CompletedTask;
            };
        }

        /// <summary>Replaces Microsoft's principal with the TGYSM session principal, or ends at the login page with an error.</summary>
        private static async Task OnTicketReceivedAsync(TicketReceivedContext context)
        {
            var handler = context.HttpContext.RequestServices.GetRequiredService<EntraSignInHandler>();
            try
            {
                var session = await handler.HandleAsync(context.Principal!, context.HttpContext.RequestAborted);
                context.Principal = SessionClaims.CreatePrincipal(session);
                context.ReturnUri = AfterSignIn;
            }
            catch (AuthenticationFailedException)
            {
                context.Response.Redirect(ErrorRedirect);
                context.HandleResponse();
            }
        }
    }

    private sealed class EntraOptionsCache(IOptionsMonitorCache<OpenIdConnectOptions> cache) : ILoginProviderCache
    {
        public void Invalidate() => cache.TryRemove(Scheme);
    }
}
