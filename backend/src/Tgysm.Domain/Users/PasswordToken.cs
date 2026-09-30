namespace Tgysm.Domain.Users;

/// <summary>
/// A single-use, time-limited password-set link (ADR-0017). Only the SHA-256 of the raw token is stored, so a
/// database read cannot be replayed as a credential. Redeeming is a conditional update on
/// <c>used_at IS NULL AND expires_at &gt; now</c> (not a read-modify-write), so two parallel redemptions of the same
/// token cannot both succeed.
/// </summary>
public sealed class PasswordToken
{
    /// <summary>Length of the SHA-256 digest, base64url-encoded without padding.</summary>
    public const int TokenHashMaxLength = 43;

    /// <summary>Spec §1 seed assumption: an activation link stays valid for 72 hours.</summary>
    public static readonly TimeSpan ActivationLifetime = TimeSpan.FromHours(72);

    /// <summary>Spec §1 seed assumption: a reset link stays valid for 1 hour.</summary>
    public static readonly TimeSpan ResetLifetime = TimeSpan.FromHours(1);

    private PasswordToken()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid UserId { get; private init; }
    public PasswordTokenPurpose Purpose { get; private init; }
    public string TokenHash { get; private init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private init; }

    /// <summary>When the token was redeemed, or voided by a newer token of the same user and purpose.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    public static TimeSpan LifetimeOf(PasswordTokenPurpose purpose) => purpose switch
    {
        PasswordTokenPurpose.Activation => ActivationLifetime,
        PasswordTokenPurpose.Reset => ResetLifetime,
        _ => throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "Unknown password token purpose."),
    };

    public static PasswordToken Issue(Guid userId, PasswordTokenPurpose purpose, string tokenHash, DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            Purpose = purpose,
            TokenHash = tokenHash,
            ExpiresAt = now + LifetimeOf(purpose),
            CreatedAt = now,
        };

    /// <summary>A newer token of the same user and purpose replaces this one (spec §4); an already-used token keeps its time.</summary>
    public void Void(DateTimeOffset now) => UsedAt ??= now;
}
