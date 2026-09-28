namespace Tgysm.Application.Common;

/// <summary>
/// Startup validation for <see cref="AppOptions"/>. A blank or relative <see cref="AppOptions.PublicBaseUrl"/>
/// would make <c>ticketUrl</c> silently render as a relative path instead of a real link in email (spec §3.2);
/// both hosts (<c>Tgysm.Api</c>, <c>Tgysm.Worker</c>) wire this into <c>OptionsBuilder.Validate</c> +
/// <c>ValidateOnStart</c> so a misconfigured deploy fails at startup instead of shipping broken links. A single
/// named, unit-testable predicate so both hosts enforce exactly the same rule.
/// </summary>
public static class AppOptionsValidation
{
    public const string PublicBaseUrlErrorMessage = "App:PublicBaseUrl must be a non-empty absolute http/https URL.";

    public static bool IsValidPublicBaseUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
