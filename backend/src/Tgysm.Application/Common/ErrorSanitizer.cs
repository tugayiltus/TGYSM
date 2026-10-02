using System.Text.RegularExpressions;
using Tgysm.Domain.Notifications;

namespace Tgysm.Infrastructure.Email;

/// <summary>
/// Turns a send failure into text safe to store as an <see cref="EmailDelivery"/>'s <c>last_error</c> (CLAUDE.md:
/// the SMTP password must never appear in logs, audit or <c>last_error</c>). Used by <see cref="SmtpEmailSender"/>
/// and <see cref="PickupFolderEmailSender"/> before they rethrow, so the exception that ever reaches Application
/// code is already safe — Application stores <c>exception.Message</c> verbatim without needing to know this type.
/// </summary>
public static partial class ErrorSanitizer
{
    private const string RedactedValue = "[redacted]";

    // Generic, not tied to the configured SMTP password's exact value: a credential-shaped key=value or key: value
    // pair anywhere in an exception's type/message is replaced, regardless of which library produced it.
    [GeneratedRegex(@"(?i)\b(password|pwd|secret|token|authorization)\b\s*[:=]\s*\S+")]
    private static partial Regex CredentialPattern();

    /// <summary>Type name + message, credential-shaped substrings redacted, truncated to <see cref="EmailDelivery.LastErrorMaxLength"/>.</summary>
    public static string Sanitize(Exception exception)
    {
        var message = $"{exception.GetType().Name}: {exception.Message}";
        message = CredentialPattern().Replace(message, match => $"{match.Groups[1].Value}: {RedactedValue}");
        return message.Length > EmailDelivery.LastErrorMaxLength ? message[..EmailDelivery.LastErrorMaxLength] : message;
    }
}
