using System.Text;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Auth.Directory;
using Tgysm.Domain.Auth;
using Tgysm.Domain.Common;

namespace Tgysm.Infrastructure.Identity;

/// <summary>
/// Active Directory over LDAPS or StartTLS with a managed client, so the Linux image needs no native LDAP library
/// (ADR-0030). Searches with the service account, then binds as the found user to check the password. Certificates are
/// validated by the platform; a plain connection is never opened.
/// </summary>
internal sealed partial class NovellLdapDirectory(ILogger<NovellLdapDirectory> logger) : ILdapDirectory
{
    private const string ObjectGuid = "objectGUID";
    private const string GivenName = "givenName";
    private const string Surname = "sn";
    private const string Mail = "mail";
    private const string UserPrincipalName = "userPrincipalName";
    private static readonly string[] Attributes = [ObjectGuid, GivenName, Surname, Mail, UserPrincipalName];

    public async Task<LdapResult> AuthenticateAsync(LdapConnectionSettings settings, string identifier, string password, CancellationToken cancellationToken)
    {
        try
        {
            using var service = await ConnectAsync(settings, cancellationToken);
            await service.BindAsync(settings.BindDn, settings.BindPassword, cancellationToken);
            var entry = await FindUserAsync(service, settings, identifier, cancellationToken);
            if (entry is null)
            {
                return LdapResult.Fail(LdapFailure.InvalidCredentials);
            }

            using var user = await ConnectAsync(settings, cancellationToken);
            try
            {
                await user.BindAsync(entry.Dn, password, cancellationToken);
            }
            catch (LdapException e) when (e.ResultCode == LdapException.InvalidCredentials)
            {
                return LdapResult.Fail(LdapFailure.InvalidCredentials);
            }

            return ToIdentity(entry) is { } identity ? LdapResult.Success(identity) : LdapResult.Fail(LdapFailure.InvalidCredentials);
        }
        catch (LdapException e)
        {
            LogUnavailable(logger, e.ResultCode, e.Message);
            return LdapResult.Fail(LdapFailure.Unavailable);
        }
    }

    public async Task<LdapTestResult> TestAsync(LdapConnectionSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            using var service = await ConnectAsync(settings, cancellationToken);
            await service.BindAsync(settings.BindDn, settings.BindPassword, cancellationToken);
            var results = await service.SearchAsync(settings.BaseDn, LdapConnection.ScopeBase, "(objectClass=*)", [], false, cancellationToken);
            await results.HasMoreAsync(cancellationToken);
            return new LdapTestResult(true, null);
        }
        catch (LdapException e) when (e.ResultCode == LdapException.InvalidCredentials)
        {
            return new LdapTestResult(false, ErrorCodes.AuthLdapBindFailed);
        }
        catch (LdapException e) when (e.ResultCode == LdapException.NoSuchObject)
        {
            return new LdapTestResult(false, ErrorCodes.AuthLdapBaseDnNotFound);
        }
        catch (LdapException e)
        {
            LogUnavailable(logger, e.ResultCode, e.Message);
            return new LdapTestResult(false, ErrorCodes.AuthDirectoryUnavailable);
        }
    }

    private static async Task<LdapConnection> ConnectAsync(LdapConnectionSettings settings, CancellationToken cancellationToken)
    {
        var connection = new LdapConnection { SecureSocketLayer = settings.Security == LdapSecurity.Ldaps };
        await connection.ConnectAsync(settings.Host, settings.Port, cancellationToken);
        if (settings.Security == LdapSecurity.StartTls)
        {
            await connection.StartTlsAsync(cancellationToken);
        }

        return connection;
    }

    private static async Task<LdapEntry?> FindUserAsync(LdapConnection connection, LdapConnectionSettings settings, string identifier, CancellationToken cancellationToken)
    {
        var value = Escape(identifier);
        var filter = $"(&(objectClass=user)(|(sAMAccountName={value})({UserPrincipalName}={value})({Mail}={value})){settings.UserFilter})";
        var results = await connection.SearchAsync(settings.BaseDn, LdapConnection.ScopeSub, filter, Attributes, false, cancellationToken);
        var entries = new List<LdapEntry>();
        while (await results.HasMoreAsync(cancellationToken))
        {
            entries.Add(await results.NextAsync(cancellationToken));
        }

        // Two entries for one name would make the sign-in ambiguous; treat it as no match.
        return entries.Count == 1 ? entries[0] : null;
    }

    private static DirectoryIdentity? ToIdentity(LdapEntry entry)
    {
        var attributes = entry.GetAttributeSet();
        var guidBytes = attributes.TryGetValue(ObjectGuid, out var guid) ? guid.ByteValue : null;
        var email = Value(attributes, Mail) ?? Value(attributes, UserPrincipalName);
        if (guidBytes is null || email is null)
        {
            return null;
        }

        return new DirectoryIdentity(LoginMethod.Ldap, new Guid(guidBytes).ToString(), email, Value(attributes, GivenName) ?? string.Empty, Value(attributes, Surname) ?? string.Empty);
    }

    private static string? Value(LdapAttributeSet attributes, string name) =>
        attributes.TryGetValue(name, out var attribute) && !string.IsNullOrWhiteSpace(attribute.StringValue) ? attribute.StringValue : null;

    /// <summary>RFC 4515 escaping, so a typed name cannot change the search filter.</summary>
    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(c switch
            {
                '\\' => @"\5c",
                '*' => @"\2a",
                '(' => @"\28",
                ')' => @"\29",
                '\0' => @"\00",
                _ => c.ToString(),
            });
        }

        return builder.ToString();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "LDAP directory unavailable ({ResultCode}): {Reason}")]
    private static partial void LogUnavailable(ILogger logger, int resultCode, string reason);
}
