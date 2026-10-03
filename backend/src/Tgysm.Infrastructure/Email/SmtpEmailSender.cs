using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Notifications;

namespace Tgysm.Infrastructure.Email;

/// <summary>Production <see cref="IEmailSender"/>: MailKit over SMTP (ADR-0015), configured under <c>Email:Smtp</c>.</summary>
internal sealed class SmtpEmailSender(IOptionsMonitor<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        var timeout = settings.Smtp.EffectiveTimeout;

        // One deadline for the whole exchange, not MailKit's per-operation default (2 minutes each): the email
        // sender's row lease is validated against this value, so a send must never outlive it.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            var mime = EmailMimeMessageFactory.Build(settings.From, message);
            using var client = new SmtpClient { Timeout = (int)timeout.TotalMilliseconds };
            await client.ConnectAsync(
                settings.Smtp.Host,
                settings.Smtp.Port,
                settings.Smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                deadline.Token);

            if (!string.IsNullOrEmpty(settings.Smtp.UserName))
            {
                await client.AuthenticateAsync(settings.Smtp.UserName, settings.Smtp.Password ?? string.Empty, deadline.Token);
            }

            await client.SendAsync(mime, deadline.Token);
            await client.DisconnectAsync(true, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own deadline, not a host shutdown: an ordinary, retryable send failure.
            throw new InvalidOperationException($"SMTP send timed out after {timeout.TotalSeconds:0} seconds.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // No inner exception kept: a caller that logs this (e.g. ILogger.LogError(exception, ...)) must never
            // be able to print the original, unsanitized message (which could echo Email:Smtp:Password) via
            // Exception.ToString()'s inner-exception chain.
            throw new InvalidOperationException(ErrorSanitizer.Sanitize(exception, EmailDelivery.LastErrorMaxLength));
        }
    }
}
