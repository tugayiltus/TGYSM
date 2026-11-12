using Tgysm.Application.Features.Auth.Settings;
using Tgysm.Domain.Auth;
using Tgysm.Application.Abstractions;
using Tgysm.Api.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Api.Security;
using Tgysm.Application.Features.Auth;
using Tgysm.Application.Features.Auth.Directory;

namespace Tgysm.Api.Endpoints;

internal static class AuthEndpoints
{
    private const string Tag = "Auth";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup(ApiRoutes.Auth).WithTags(Tag);

        auth.MapGet("/csrf", GetCsrfToken).AllowAnonymous();
        auth.MapGet("/method", async (GetLoginMethodHandler handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct))).AllowAnonymous();
        auth.MapGet("/oidc/start", StartEntraAsync).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Login);
        auth.MapPost("/login/ldap", LdapLoginAsync).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Login)
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        auth.MapPost("/login", LoginAsync).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Login)
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status429TooManyRequests);
        auth.MapPost("/logout", (Delegate)LogoutAsync).RequireAuthorization(Policies.PasswordChangePending);
        auth.MapGet("/me", GetMeAsync).RequireAuthorization(Policies.PasswordChangePending)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        auth.MapPost("/forgot-password", ForgotPasswordAsync).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.ForgotPassword)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        auth.MapPost("/set-password", SetPasswordAsync).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.SetPassword)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status429TooManyRequests);
        auth.MapPost("/change-password", ChangePasswordAsync).RequireAuthorization(Policies.PasswordChangePending)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return api;
    }

    private static Ok<CsrfTokenResponse> GetCsrfToken(HttpContext context, IAntiforgery antiforgery) =>
        TypedResults.Ok(new CsrfTokenResponse(antiforgery.GetAndStoreTokens(context).RequestToken!));

    private static async Task<Ok<LoginResponse>> LoginAsync(LoginRequest request, LoginHandler handler, HttpContext context, CancellationToken cancellationToken)
    {
        var session = await handler.HandleAsync(new LoginCommand(request.Email, request.Password), cancellationToken);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, SessionClaims.CreatePrincipal(session));
        return TypedResults.Ok(new LoginResponse(session.MustChangePassword));
    }

    /// <summary>"Microsoft ile giriş yap": a full-page navigation that redirects to Microsoft; 404 unless Entra is the method.</summary>
    private static async Task<IResult> StartEntraAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var settings = await db.AuthSettings.AsNoTracking().SingleAsync(cancellationToken);
        return settings.Method == LoginMethod.Entra
            ? Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }, [EntraAuthentication.Scheme])
            : Results.NotFound();
    }

    private static async Task<Ok<LoginResponse>> LdapLoginAsync(LdapLoginRequest request, LdapLoginHandler handler, HttpContext context, CancellationToken cancellationToken)
    {
        var session = await handler.HandleAsync(new LdapLoginCommand(request.Identifier, request.Password), cancellationToken);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, SessionClaims.CreatePrincipal(session));
        return TypedResults.Ok(new LoginResponse(session.MustChangePassword));
    }

    private static async Task<NoContent> LogoutAsync(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<MeDto>> GetMeAsync(GetMeHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    /// <summary>Always 202 with no body, whether or not the email names an active account (spec §4: no enumeration).</summary>
    private static async Task<Accepted> ForgotPasswordAsync(ForgotPasswordRequest request, ForgotPasswordHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new ForgotPasswordCommand(request.Email), cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    /// <summary>Does not sign in: the user logs in with the new password afterwards.</summary>
    private static async Task<NoContent> SetPasswordAsync(SetPasswordRequest request, SetPasswordHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new SetPasswordCommand(request.Token, request.NewPassword), cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Re-issues the cookie with the new security stamp so this session survives the rotation.</summary>
    private static async Task<NoContent> ChangePasswordAsync(
        ChangePasswordCommand command, ChangePasswordHandler handler, HttpContext context, CancellationToken cancellationToken)
    {
        var session = await handler.HandleAsync(command, cancellationToken);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, SessionClaims.CreatePrincipal(session));
        return TypedResults.NoContent();
    }
}
