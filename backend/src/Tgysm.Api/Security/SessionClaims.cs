using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Tgysm.Application.Features.Auth;
using Tgysm.Domain.Users;

namespace Tgysm.Api.Security;

internal static class SessionClaims
{
    public const string SecurityStamp = "tgysm:security_stamp";
    public const string MustChangePassword = "tgysm:must_change_password";

    public static ClaimsPrincipal CreatePrincipal(SessionUser session)
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new(ClaimTypes.Role, UserRoleNames.ToName(session.Role)),
            new(SecurityStamp, session.SecurityStamp),
            new(MustChangePassword, session.MustChangePassword ? bool.TrueString : bool.FalseString),
        ];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
