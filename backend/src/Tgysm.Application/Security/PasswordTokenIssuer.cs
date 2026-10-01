using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Security;

/// <summary>
/// Issues a password-set link (ADR-0017): voids the user's unused tokens of the same purpose, stores only the
/// SHA-256 of a fresh random token, and queues the activation/reset email with the raw token as the outbox event's
/// protected secret (ADR-0015). Adds to the caller's unit of work; the caller saves once.
/// </summary>
public sealed class PasswordTokenIssuer(
    IAppDbContext db,
    INotificationOutbox outbox,
    IAccountLinkProtector protector,
    IAuditLog audit,
    TimeProvider clock)
{
    /// <summary>Spec §4: the token is 32 random bytes, base64url.</summary>
    public const int TokenBytes = 32;

    private const string RoundTripDateFormat = "O";

    public async Task<string> IssueAsync(Guid userId, PasswordTokenPurpose purpose, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var unused = await db.PasswordTokens
            .Where(t => t.UserId == userId && t.Purpose == purpose && t.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var previous in unused)
        {
            previous.Void(now);
        }

        var rawToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        var token = PasswordToken.Issue(userId, purpose, Hash(rawToken), now);
        db.PasswordTokens.Add(token);

        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [OutboxEvent.LinkExpiresAtKey] = token.ExpiresAt.ToString(RoundTripDateFormat, CultureInfo.InvariantCulture),
        };
        outbox.Add(EventTypeOf(purpose), ticketId: null, userId, payload, protector.Protect(rawToken));
        audit.Record(AuditActionOf(purpose), AuditEntityTypes.User, userId);
        return rawToken;
    }

    /// <summary>
    /// Admin retry of a failed account email (final review F4): the original token's secret was cleared when the
    /// delivery failed, so a new token (same purpose, fresh expiry) is issued onto the same outbox event; every other
    /// unused token of the user is voided. Adds to the caller's unit of work; the caller saves once.
    /// </summary>
    public async Task ReissueAsync(OutboxEvent outboxEvent, Guid userId, PasswordTokenPurpose purpose, CancellationToken cancellationToken)
    {
        await RevokeUnusedAsync(userId, cancellationToken);
        var rawToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        var token = PasswordToken.Issue(userId, purpose, Hash(rawToken), clock.GetUtcNow());
        db.PasswordTokens.Add(token);
        outboxEvent.RenewAccountLink(protector.Protect(rawToken), token.ExpiresAt);
        audit.Record(AuditActionOf(purpose), AuditEntityTypes.User, userId);
    }

    /// <summary>The token purpose behind an account-link event type (<c>account_activation</c> or <c>password_reset</c>).</summary>
    public static PasswordTokenPurpose PurposeOf(string eventType) => eventType switch
    {
        NotificationEventTypes.AccountActivation => PasswordTokenPurpose.Activation,
        NotificationEventTypes.PasswordReset => PasswordTokenPurpose.Reset,
        _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, "Not an account-link event type."),
    };

    /// <summary>
    /// Voids every unused token of the user, of any purpose (final review F5): after a password is set, the email
    /// address changes or a temporary password replaces the old one, no link issued before may still work. Adds to
    /// the caller's unit of work; the caller saves once.
    /// </summary>
    public async Task RevokeUnusedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var unused = await db.PasswordTokens.Where(t => t.UserId == userId && t.UsedAt == null).ToListAsync(cancellationToken);
        foreach (var token in unused)
        {
            token.Void(now);
        }
    }

    /// <summary>The stored form of a raw token: SHA-256 over its UTF-8 bytes, base64url.</summary>
    public static string Hash(string rawToken) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private static string EventTypeOf(PasswordTokenPurpose purpose) => purpose switch
    {
        PasswordTokenPurpose.Activation => NotificationEventTypes.AccountActivation,
        PasswordTokenPurpose.Reset => NotificationEventTypes.PasswordReset,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Unknown password token purpose."),
    };

    private static string AuditActionOf(PasswordTokenPurpose purpose) => purpose switch
    {
        PasswordTokenPurpose.Activation => AuditActions.UserActivationLinkSent,
        PasswordTokenPurpose.Reset => AuditActions.UserPasswordResetLinkSent,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Unknown password token purpose."),
    };
}
