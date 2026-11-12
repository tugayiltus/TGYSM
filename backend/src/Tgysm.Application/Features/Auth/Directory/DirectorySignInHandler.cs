using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Auth;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Auth.Directory;

/// <summary>
/// Turns a verified directory identity into a TGYSM session (ADR-0030): match by the stored directory id, else link the
/// user with the same email, else create an employee. Names and email follow the directory; role, organization and any
/// local password stay. Every refusal throws <see cref="AuthenticationFailedException"/> and is audited.
/// </summary>
public sealed class DirectorySignInHandler(IAppDbContext db, IAuditLog audit, TimeProvider clock)
{
    public async Task<SessionUser> HandleAsync(DirectoryIdentity identity, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var email = User.NormalizeEmail(identity.Email);
        var user = await FindLinkedAsync(identity, cancellationToken);
        if (user is null)
        {
            user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == email, cancellationToken);
            if (user is null)
            {
                user = User.CreateFromDirectory(identity.FirstName, identity.LastName, identity.Email, now);
                db.Users.Add(user);
                audit.Record(AuditActions.UserCreatedFromDirectory, AuditEntityTypes.User, user.Id, actorUserId: user.Id,
                    newValues: new { user.Email, Source = identity.Source });
            }

            Link(user, identity, now);
        }

        if (!user.CanSignIn(now))
        {
            await RefuseAsync(user.Id, identity.Source, cancellationToken);
        }

        // An email the directory moved to another TGYSM user would merge two people; refuse until an admin resolves it.
        if (user.NormalizedEmail != email && await db.Users.AnyAsync(u => u.Id != user.Id && u.NormalizedEmail == email, cancellationToken))
        {
            await RefuseAsync(user.Id, identity.Source, cancellationToken);
        }

        user.RefreshFromDirectory(identity.FirstName, identity.LastName, identity.Email, now);
        user.RegisterSuccessfulSignIn(now);
        audit.Record(AuditActions.DirectorySignIn, AuditEntityTypes.User, user.Id, actorUserId: user.Id, newValues: new { Source = identity.Source });
        await db.SaveChangesAsync(cancellationToken);
        return SessionUser.From(user);
    }

    private Task<User?> FindLinkedAsync(DirectoryIdentity identity, CancellationToken cancellationToken) => identity.Source switch
    {
        LoginMethod.Entra => db.Users.SingleOrDefaultAsync(u => u.EntraObjectId == identity.ExternalId, cancellationToken),
        LoginMethod.Ldap when Guid.TryParse(identity.ExternalId, out var guid) => db.Users.SingleOrDefaultAsync(u => u.LdapObjectGuid == guid, cancellationToken),
        _ => Task.FromResult<User?>(null),
    };

    private static void Link(User user, DirectoryIdentity identity, DateTimeOffset now)
    {
        if (identity.Source == LoginMethod.Entra)
        {
            user.LinkEntra(identity.ExternalId, now);
        }
        else if (identity.Source == LoginMethod.Ldap && Guid.TryParse(identity.ExternalId, out var guid))
        {
            user.LinkLdap(guid, now);
        }
    }

    private async Task RefuseAsync(Guid userId, LoginMethod source, CancellationToken cancellationToken)
    {
        audit.Record(AuditActions.LoginFailed, AuditEntityTypes.User, userId, newValues: new { Source = source });
        await db.SaveChangesAsync(cancellationToken);
        throw new AuthenticationFailedException();
    }
}
