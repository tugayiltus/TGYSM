namespace Tgysm.Infrastructure.Email;

/// <summary>Valid values for <see cref="EmailOptions.Mode"/> (spec §3.5).</summary>
public static class EmailModes
{
    public const string Smtp = "Smtp";
    public const string Pickup = "Pickup";
}

/// <summary>
/// Root of the <c>Email</c> configuration section (spec §3.5, ADR-0015). <see cref="Mode"/> selects which
/// <c>IEmailSender</c> is registered; a missing or unrecognized value throws at resolution time and, in
/// <c>Tgysm.Worker</c>, fails the host at startup (<c>ValidateOnStart</c>) rather than at the first send.
/// <see cref="Smtp"/>'s <see cref="SmtpOptions.Password"/> must come from user-secrets or the environment only —
/// never appsettings, never logged, audited, or stored.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>One of <see cref="EmailModes"/>.</summary>
    public string? Mode { get; set; }

    /// <summary>The envelope "From" address (and optional display name), e.g. <c>"TGYSM &lt;no-reply@tgysm.local&gt;"</c>.</summary>
    public string From { get; set; } = string.Empty;

    public SmtpOptions Smtp { get; set; } = new();

    /// <summary>Folder <see cref="PickupFolderEmailSender"/> writes <c>.eml</c> files into; required when <see cref="Mode"/> is <see cref="EmailModes.Pickup"/>.</summary>
    public string? PickupFolder { get; set; }
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public bool UseStartTls { get; set; }

    public string? UserName { get; set; }

    /// <summary>Secret: user-secrets / environment only (CLAUDE.md). Never appears in appsettings, logs, audit, or a delivery's <c>last_error</c>.</summary>
    public string? Password { get; set; }
}
