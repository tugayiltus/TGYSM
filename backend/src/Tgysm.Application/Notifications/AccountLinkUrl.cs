using System.Globalization;

namespace Tgysm.Application.Notifications;

/// <summary>
/// Builds the link an account-activation/password-reset email points to (ADR-0017): <c>App:PublicBaseUrl</c> plus
/// a fixed path shape carrying the token as a query parameter. Task 7's <c>/auth/set-password</c> UI route reads
/// the same <c>token</c> parameter this path writes.
/// </summary>
public static class AccountLinkUrl
{
    private const string SetPasswordPathFormat = "/account/set-password?token={0}";

    public static string Build(string publicBaseUrl, string token) =>
        publicBaseUrl.TrimEnd('/') + string.Format(CultureInfo.InvariantCulture, SetPasswordPathFormat, Uri.EscapeDataString(token));
}
