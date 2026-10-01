using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Tgysm.Application.Abstractions;
using Tgysm.Infrastructure.Email;

namespace Tgysm.Api.IntegrationTests.Notifications;

/// <summary>
/// A black-holed SMTP server (accepts the connection, never answers) must fail the send within
/// <c>Email:Smtp:TimeoutSeconds</c>, as an ordinary send failure, so the email sender's row lease outlives the send.
/// </summary>
public sealed class SmtpEmailSenderTimeoutTests
{
    private const int TimeoutSeconds = 1;
    private static readonly TimeSpan UpperBound = TimeSpan.FromSeconds(10);

    private sealed class FixedOptionsMonitor(EmailOptions value) : IOptionsMonitor<EmailOptions>
    {
        public EmailOptions CurrentValue => value;

        public EmailOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<EmailOptions, string?> listener) => null;
    }

    [Fact]
    public async Task A_server_that_never_answers_fails_the_send_within_the_configured_timeout()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var options = new EmailOptions
        {
            Mode = EmailModes.Smtp,
            From = "TGYSM <no-reply@tgysm.local>",
            Smtp = new SmtpOptions { Host = IPAddress.Loopback.ToString(), Port = port, UseStartTls = false, TimeoutSeconds = TimeoutSeconds },
        };
        var sender = new SmtpEmailSender(new FixedOptionsMonitor(options));
        var message = new EmailMessage("user@example.com", "Ayşe Yılmaz", "Konu", "Gövde");

        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(message, TestContext.Current.CancellationToken));

        Assert.True(stopwatch.Elapsed < UpperBound, $"Send took {stopwatch.Elapsed} with a {TimeoutSeconds}s timeout.");
    }
}
