using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Auth;

namespace Tgysm.Application.Features.Auth.Directory;

/// <summary>
/// Microsoft Entra sign-in after the token was validated (ADR-0030): only while Entra is the login method and only for an
/// identity of the configured tenant with an allowed email domain. Refusals are audited and throw
/// <see cref="AuthenticationFailedException"/>.
/// </summary>
public sealed class EntraSignInHandler(IAppDbContext db, DirectorySignInHandler signIn, IAuditLog audit)
{
    public async Task<SessionUser> HandleAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var settings = await db.AuthSettings.AsNoTracking().SingleAsync(cancellationToken);
        var identity = settings.Method == LoginMethod.Entra ? EntraIdentityMapper.Map(principal, settings.EntraTenantId, settings.EntraAllowedDomains) : null;
        if (identity is null)
        {
            audit.Record(AuditActions.LoginFailed, AuditEntityTypes.User, entityId: null,
                newValues: new { Source = LoginMethod.Entra, Tenant = principal.FindFirst(EntraIdentityMapper.TenantClaim)?.Value });
            await db.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException();
        }

        return await signIn.HandleAsync(identity, cancellationToken);
    }
}
