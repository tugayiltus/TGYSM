using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Security;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Auth;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Auth.Directory;

/// <summary>Windows user name (<c>sAMAccountName</c>) or email (<c>userPrincipalName</c> / <c>mail</c>), and the AD password.</summary>
public sealed record LdapLoginCommand(string Identifier, string Password);

internal sealed class LdapLoginValidator : AbstractValidator<LdapLoginCommand>
{
    public LdapLoginValidator()
    {
        RuleFor(c => c.Identifier).NotEmpty().MaximumLength(User.EmailMaxLength);
        RuleFor(c => c.Password).NotEmpty().MaximumLength(PasswordRules.MaxLength);
    }
}

/// <summary>
/// Active Directory sign-in (ADR-0030): only while LDAP is the login method. Every failure answers like an unknown name;
/// the throttle counts attempts per typed name and per found account. The directory's answer goes through <see cref="DirectorySignInHandler"/>.
/// </summary>
public sealed class LdapLoginHandler(
    IAppDbContext db,
    IValidator<LdapLoginCommand> validator,
    ILdapDirectory directory,
    ILdapSignInThrottle throttle,
    IAuthSecretProtector secrets,
    DirectorySignInHandler signIn,
    IAuditLog audit,
    TimeProvider clock)
{
    private const string KeyPrefix = "ldap:";
    private const string AccountKeyPrefix = "ldap-account:";

    public async Task<SessionUser> HandleAsync(LdapLoginCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var now = clock.GetUtcNow();
        var identifier = command.Identifier.Trim();
        var key = KeyPrefix + identifier.ToLowerInvariant();
        var settings = await db.AuthSettings.AsNoTracking().SingleAsync(cancellationToken);
        if (settings.Method != LoginMethod.Ldap || settings.LdapBindPasswordProtected is null)
        {
            throw new AuthenticationFailedException();
        }

        // Count the typed name first, then the found account, so other spellings of one account share its limit.
        if (!await throttle.TryAttemptAsync(key, now, cancellationToken))
        {
            await FailAsync(identifier, cancellationToken);
        }

        var connection = new LdapConnectionSettings(settings.LdapHost, settings.LdapPort, settings.LdapSecurity, settings.LdapBaseDn, settings.LdapBindDn,
            secrets.Unprotect(settings.LdapBindPasswordProtected), settings.LdapUserFilter);
        var match = await directory.FindAsync(connection, identifier, cancellationToken);
        ThrowIfUnavailable(match.Status);
        if (match.Status != LdapStatus.Ok || match.Identity is null || match.DistinguishedName is null)
        {
            await FailAsync(identifier, cancellationToken);
        }

        var accountKey = AccountKeyPrefix + match.Identity!.ExternalId;
        if (!await throttle.TryAttemptAsync(accountKey, now, cancellationToken))
        {
            await FailAsync(identifier, cancellationToken);
        }

        var password = await directory.CheckPasswordAsync(connection, match.DistinguishedName!, command.Password, cancellationToken);
        ThrowIfUnavailable(password);
        if (password != LdapStatus.Ok)
        {
            await FailAsync(identifier, cancellationToken);
        }

        await throttle.ResetAsync(key, cancellationToken);
        await throttle.ResetAsync(accountKey, cancellationToken);
        return await signIn.HandleAsync(match.Identity, cancellationToken);
    }

    private static void ThrowIfUnavailable(LdapStatus status)
    {
        if (status == LdapStatus.Unavailable)
        {
            throw new DirectoryUnavailableException();
        }
    }

    private async Task FailAsync(string identifier, CancellationToken cancellationToken)
    {
        audit.Record(AuditActions.LoginFailed, AuditEntityTypes.User, entityId: null, newValues: new { Identifier = identifier, Source = LoginMethod.Ldap });
        await db.SaveChangesAsync(cancellationToken);
        throw new AuthenticationFailedException();
    }
}
