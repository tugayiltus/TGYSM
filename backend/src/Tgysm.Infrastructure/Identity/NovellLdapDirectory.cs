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
/// (ADR-0030). Finds users with the service account; checks a password by binding as the found entry. Certificates are
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

    /// <summary>Each lookup or password check gives up after this, so a silent domain controller cannot hold a sign-in for minutes.</summary>
    public static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);

    public async Task<LdapEntryMatch> FindAsync(LdapConnectionSettings settings, string identifier, CancellationToken callerToken)
    {
        using var timeout = Timeout(callerToken);
        var cancellationToken = timeout.Token;
        try
        {
            using var service = await ConnectAsync(settings, cancellationToken);
            await service.BindAsync(settings.BindDn, settings.BindPassword, cancellationToken);
            var entry = await FindUserAsync(service, settings, identifier, cancellationToken);
            return entry is not null && ToIdentity(entry) is { } identity
                ? new LdapEntryMatch(LdapStatus.Ok, entry.Dn, identity)
                : new LdapEntryMatch(LdapStatus.NotFound, null, null);
        }
        catch (Exception e) when (e is LdapException || IsTimeout(e, callerToken))
        {
            LogUnavailable(logger, (e as LdapException)?.ResultCode ?? 0, e.Message);
            return new LdapEntryMatch(LdapStatus.Unavailable, null, null);
        }
    }

    public async Task<LdapStatus> CheckPasswordAsync(LdapConnectionSettings settings, string distinguishedName, string password, CancellationToken callerToken)
    {
        using var timeout = Timeout(callerToken);
        var cancellationToken = timeout.Token;
        try
        {
            using var user = await ConnectAsync(settings, cancellationToken);
            await user.BindAsync(distinguishedName, password, cancellationToken);
            return LdapStatus.Ok;
        }
        catch (LdapException e) when (e.ResultCode == LdapException.InvalidCredentials)
        {
            return LdapStatus.InvalidCredentials;
        }
        catch (Exception e) when (e is LdapException || IsTimeout(e, callerToken))
        {
            LogUnavailable(logger, (e as LdapException)?.ResultCode ?? 0, e.Message);
            return LdapStatus.Unavailable;
        }
    }

    public async Task<LdapTestResult> TestAsync(LdapConnectionSettings settings, CancellationToken callerToken)
    {
        using var timeout = Timeout(callerToken);
        var cancellationToken = timeout.Token;
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
        catch (Exception e) when (e is LdapException || IsTimeout(e, callerToken))
        {
            LogUnavailable(logger, (e as LdapException)?.ResultCode ?? 0, e.Message);
            return new LdapTestResult(false, ErrorCodes.AuthDirectoryUnavailable);
        }
    }

    private static CancellationTokenSource Timeout(CancellationToken callerToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        timeout.CancelAfter(OperationTimeout);
        return timeout;
    }

    /// <summary>Our own time limit ran out (not the caller giving up), or the connect attempt timed out on the socket.</summary>
    private static bool IsTimeout(Exception e, CancellationToken callerToken) =>
        (e is OperationCanceledException && !callerToken.IsCancellationRequested) || e is TimeoutException;

    private static async Task<LdapConnection> ConnectAsync(LdapConnectionSettings settings, CancellationToken cancellationToken)
    {
        var connection = new LdapConnection
        {
            SecureSocketLayer = settings.Security == LdapSecurity.Ldaps,
            ConnectionTimeout = (int)OperationTimeout.TotalMilliseconds,
        };
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
            try
            {
                entries.Add(await results.NextAsync(cancellationToken));
            }
            catch (LdapReferralException)
            {
                // A search at the domain root also returns references to other naming contexts (DomainDnsZones,
                // ForestDnsZones, Configuration); users never live there, so they are skipped, not followed.
            }
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
