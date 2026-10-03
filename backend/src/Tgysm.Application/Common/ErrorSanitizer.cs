using System.Text.RegularExpressions;

namespace Tgysm.Application.Common;

/// <summary>
/// Turns a failure into text safe to store in a <c>last_error</c> column (CLAUDE.md: the SMTP password must never appear in
/// logs, audit or <c>last_error</c>). The email senders (Infrastructure) use it before they rethrow, so the exception that
/// reaches the email use cases is already safe; the export runner uses it before it records a failed export job.
/// </summary>
public static partial class ErrorSanitizer
{
    private const string RedactedValue = "[redacted]";

    // Generic, not tied to the configured SMTP password's exact value: a credential-shaped key=value or key: value
    // pair anywhere in an exception's type/message is replaced, regardless of which library produced it.
    [GeneratedRegex(@"(?i)\b(password|pwd|secret|token|authorization)\b\s*[:=]\s*\S+")]
    private static partial Regex CredentialPattern();

    /// <summary>Type name + message, credential-shaped substrings redacted, truncated to <paramref name="maxLength"/>.</summary>
    public static string Sanitize(Exception exception, int maxLength)
    {
        var message = $"{exception.GetType().Name}: {exception.Message}";
        message = CredentialPattern().Replace(message, match => $"{match.Groups[1].Value}: {RedactedValue}");
        return message.Length > maxLength ? message[..maxLength] : message;
    }
}
