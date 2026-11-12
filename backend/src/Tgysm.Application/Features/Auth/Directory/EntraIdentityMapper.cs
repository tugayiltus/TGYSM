using System.Security.Claims;
using Tgysm.Domain.Auth;

namespace Tgysm.Application.Features.Auth.Directory;

/// <summary>
/// Reads a verified Microsoft Entra ID token (ADR-0030). Only the configured tenant is accepted, and the email domain must
/// be in the allowed list (an empty list accepts every domain of the tenant). Returns null when any check fails.
/// </summary>
public static class EntraIdentityMapper
{
    public const string TenantClaim = "tid";
    public const string ObjectIdClaim = "oid";
    private const string EmailClaim = "email";
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
        var email = Value(principal, EmailClaim) ?? Value(principal, PreferredUserNameClaim) ?? Value(principal, UpnClaim);
        if (tenant is null || !string.Equals(tenant, configuredTenantId, StringComparison.OrdinalIgnoreCase) || objectId is null || email is null)
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
