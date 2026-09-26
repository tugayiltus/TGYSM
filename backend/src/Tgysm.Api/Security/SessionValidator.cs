using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Tgysm.Application.Features.Auth;

namespace Tgysm.Api.Security;

/// <summary>
/// Runs on every request: rejects the cookie when the user is inactive or the security stamp changed,
/// otherwise refreshes role and must-change-password claims from the database (ADR-0004).
/// </summary>
internal static class SessionValidator
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var stamp = principal?.FindFirstValue(SessionClaims.SecurityStamp);
        if (stamp is null || !Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await RejectAsync(context);
            return;
        }

        var handler = context.HttpContext.RequestServices.GetRequiredService<GetSessionHandler>();
        var session = await handler.HandleAsync(new GetSessionQuery(userId), context.HttpContext.RequestAborted);
        if (session is null || !string.Equals(session.SecurityStamp, stamp, StringComparison.Ordinal))
        {
            await RejectAsync(context);
            return;
        }

        context.ReplacePrincipal(SessionClaims.CreatePrincipal(session));
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
