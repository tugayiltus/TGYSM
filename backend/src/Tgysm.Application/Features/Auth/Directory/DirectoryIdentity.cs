using Tgysm.Domain.Auth;

namespace Tgysm.Application.Features.Auth.Directory;

/// <summary>
/// A person the directory has verified (Entra token or LDAP bind). <see cref="ExternalId"/> is the Entra <c>oid</c> or the
/// AD <c>objectGUID</c>. Callers check that the identity belongs to the company before passing it on (ADR-0030).
/// </summary>
public sealed record DirectoryIdentity(LoginMethod Source, string ExternalId, string Email, string FirstName, string LastName);
