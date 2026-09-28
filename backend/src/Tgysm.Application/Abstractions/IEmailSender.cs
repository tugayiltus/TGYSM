namespace Tgysm.Application.Abstractions;

/// <summary>One rendered email ready to hand to a transport; <see cref="Body"/> is plain text (ADR-0016, no HTML).</summary>
public sealed record EmailMessage(string To, string ToName, string Subject, string Body);

/// <summary>
/// Sends one already-rendered email (ADR-0015). Two adapters exist: MailKit SMTP in production, a pickup folder
/// (<c>.eml</c> files) in development and tests — no live SMTP dependency for local runs or CI. Implementations
/// must sanitize any thrown exception's message (no SMTP credentials) before it reaches <c>SendDueEmailsHandler</c>,
/// which stores it verbatim as the delivery's <c>last_error</c>.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
