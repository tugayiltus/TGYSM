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

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (FailWith is { } failWith)
        {
            throw failWith(message);
        }

        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public void Reset()
    {
        FailWith = null;
        _sent.Clear();
    }
}
