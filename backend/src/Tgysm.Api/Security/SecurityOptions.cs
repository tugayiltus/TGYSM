namespace Tgysm.Api.Security;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";
    public const int DefaultLoginPermitLimitPerMinute = 10;
    public const int DefaultSessionHours = 8;
    public const int DefaultForgotPasswordPermitLimitPerMinute = 5;
    public const int DefaultSetPasswordPermitLimitPerMinute = 5;

    public int LoginPermitLimitPerMinute { get; init; } = DefaultLoginPermitLimitPerMinute;

    public int SessionHours { get; init; } = DefaultSessionHours;

    /// <summary>Forgot-password requests per client IP per minute (spec §4: rate limited per IP).</summary>
    public int ForgotPasswordPermitLimitPerMinute { get; init; } = DefaultForgotPasswordPermitLimitPerMinute;

    /// <summary>Set-password requests per client IP per minute: each one hashes a password before the token lookup.</summary>
    public int SetPasswordPermitLimitPerMinute { get; init; } = DefaultSetPasswordPermitLimitPerMinute;
}
