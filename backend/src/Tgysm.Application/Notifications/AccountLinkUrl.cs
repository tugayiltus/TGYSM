using System.Globalization;

namespace Tgysm.Application.Notifications;

/// <summary>
/// Builds the link an account-activation/password-reset email points to (ADR-0017): <c>App:PublicBaseUrl</c> plus
/// a fixed path shape carrying the token as a query parameter. The <c>/set-password</c> UI route (spec §4) reads
/// the same <c>token</c> parameter this path writes.
/// </summary>
public static class AccountLinkUrl
{
    public const string SetPasswordPathFormat = "/set-password?token={0}";

    public static string Build(string publicBaseUrl, string token) =>
        publicBaseUrl.TrimEnd('/') + string.Format(CultureInfo.InvariantCulture, SetPasswordPathFormat, Uri.EscapeDataString(token));
}
