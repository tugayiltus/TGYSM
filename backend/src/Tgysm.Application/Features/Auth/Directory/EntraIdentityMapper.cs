using System.Security.Claims;
using Tgysm.Domain.Auth;

namespace Tgysm.Application.Features.Auth.Directory;

/// <summary>
/// Reads a verified Microsoft Entra ID token (ADR-0030). Only members of the configured tenant are accepted: guests from
/// another organization (an <c>idp</c> claim) are refused. The sign-in name (<c>preferred_username</c>, else <c>upn</c>)
/// comes from the tenant's verified domains and is used as the email; the <c>email</c> claim is never used, because it is
/// not verified and could name another person's account (nOAuth). Its domain must be in the allowed list (an empty list
/// accepts every domain of the tenant). Returns null when any check fails.
/// </summary>
public static class EntraIdentityMapper
{
    public const string TenantClaim = "tid";
    public const string ObjectIdClaim = "oid";
    private const string IdentityProviderClaim = "idp";
    /// <summary>Optional claim: 0 member, 1 guest.</summary>
    private const string AccountTypeClaim = "acct";
    private const string GuestAccountType = "1";
    /// <summary>Guest sign-in names carry this marker (<c>name_domain#EXT#@tenant</c>).</summary>
    private const string GuestMarker = "#EXT#";
    private const string TenantIssuerFormat = "https://sts.windows.net/{0}/";
    private const string PreferredUserNameClaim = "preferred_username";
    private const string UpnClaim = "upn";
    private const string GivenNameClaim = "given_name";
    private const string FamilyNameClaim = "family_name";
    private const string NameClaim = "name";
    private const char DomainSeparator = '@';
    private const char NameSeparator = ' ';

    public static DirectoryIdentity? Map(ClaimsPrincipal principal, string configuredTenantId, IReadOnlyList<string> allowedDomains)
    {
        var tenant = Value(principal, TenantClaim);
        var objectId = Value(principal, ObjectIdClaim);
        var email = Value(principal, PreferredUserNameClaim) ?? Value(principal, UpnClaim);
        var guest = IsGuest(principal, configuredTenantId, email);
        if (tenant is null || !string.Equals(tenant, configuredTenantId, StringComparison.OrdinalIgnoreCase) || objectId is null || email is null || guest)
        {
            return null;
        }

        var at = email.LastIndexOf(DomainSeparator);
        var domain = at < 0 ? string.Empty : email[(at + 1)..].ToLowerInvariant();
        if (domain.Length == 0 || (allowedDomains.Count > 0 && !allowedDomains.Contains(domain)))
        {
            return null;
        }

        var (first, last) = Names(principal);
        return new DirectoryIdentity(LoginMethod.Entra, objectId, email, first, last);
    }

    /// <summary>
    /// A guest is refused whatever the email: an <c>idp</c> other than the tenant's own issuer, <c>acct</c> = 1, or a
    /// <c>#EXT#</c> sign-in name. Configure <c>acct</c> and <c>idp</c> as optional claims in the Entra app for the strictest check.
    /// </summary>
    private static bool IsGuest(ClaimsPrincipal principal, string configuredTenantId, string? email)
    {
        var ownIssuer = string.Format(System.Globalization.CultureInfo.InvariantCulture, TenantIssuerFormat, configuredTenantId);
        var foreignIdp = Value(principal, IdentityProviderClaim) is { } idp && !string.Equals(idp, ownIssuer, StringComparison.OrdinalIgnoreCase);
        var guestAccount = Value(principal, AccountTypeClaim) == GuestAccountType;
        var externalName = (email ?? string.Empty).Contains(GuestMarker, StringComparison.OrdinalIgnoreCase)
            || (Value(principal, UpnClaim) ?? string.Empty).Contains(GuestMarker, StringComparison.OrdinalIgnoreCase);
        return foreignIdp || guestAccount || externalName;
    }

    private static (string First, string Last) Names(ClaimsPrincipal principal)
    {
        if (Value(principal, GivenNameClaim) is { } given && Value(principal, FamilyNameClaim) is { } family)
        {
            return (given, family);
        }

        var display = (Value(principal, NameClaim) ?? string.Empty).Trim();
        var split = display.LastIndexOf(NameSeparator);
        return split < 0 ? (display, string.Empty) : (display[..split], display[(split + 1)..]);
    }

    private static string? Value(ClaimsPrincipal principal, string type) =>
        principal.FindFirst(type)?.Value is { Length: > 0 } value ? value.Trim() : null;
}
