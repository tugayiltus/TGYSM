using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Api.Security;
using Tgysm.Application.Features.Auth;

namespace Tgysm.Api.Endpoints;

internal static class AuthEndpoints
{
    private const string Tag = "Auth";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup(ApiRoutes.Auth).WithTags(Tag);

        auth.MapGet("/csrf", GetCsrfToken).AllowAnonymous();
        auth.MapPost("/login", LoginAsync).AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Login)
            .ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status429TooManyRequests);
        auth.MapPost("/logout", (Delegate)LogoutAsync).RequireAuthorization(Policies.PasswordChangePending);
        auth.MapGet("/me", GetMeAsync).RequireAuthorization(Policies.PasswordChangePending)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
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

    private static async Task<NoContent> LogoutAsync(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<MeDto>> GetMeAsync(GetMeHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));

    /// <summary>Re-issues the cookie with the new security stamp so this session survives the rotation.</summary>
    private static async Task<NoContent> ChangePasswordAsync(
        ChangePasswordCommand command, ChangePasswordHandler handler, HttpContext context, CancellationToken cancellationToken)
    {
        var session = await handler.HandleAsync(command, cancellationToken);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, SessionClaims.CreatePrincipal(session));
        return TypedResults.NoContent();
    }
}
