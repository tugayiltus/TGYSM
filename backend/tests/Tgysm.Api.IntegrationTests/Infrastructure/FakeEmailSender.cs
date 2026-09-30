using System.Collections.Concurrent;
using Tgysm.Application.Abstractions;

namespace Tgysm.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Test double for <see cref="IEmailSender"/> registered in <see cref="ApiFactory"/> in place of the real
/// Pickup/SMTP senders, so tests can force a send to fail without a network dependency (task brief: "fake failing
/// IEmailSender"). Default behavior is "succeed silently" so tests unrelated to email sending never notice it.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    /// <summary>Set by a test to make the next (and every following, until <see cref="Reset"/>) send throw.</summary>
    public Func<EmailMessage, Exception>? FailWith { get; set; }

    /// <summary>Runs before each send; lets a test interfere with the database while the transport call is in flight.</summary>
    public Func<EmailMessage, Task>? BeforeSend { get; set; }

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (BeforeSend is { } beforeSend)
        {
            await beforeSend(message);
        }

        if (FailWith is { } failWith)
        {
            throw failWith(message);
        }

        _sent.Enqueue(message);
    }

    public void Reset()
    {
        FailWith = null;
        BeforeSend = null;
        _sent.Clear();
    }
}
