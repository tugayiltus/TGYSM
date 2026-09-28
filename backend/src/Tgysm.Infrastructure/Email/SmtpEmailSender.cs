using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using Tgysm.Application.Abstractions;

namespace Tgysm.Infrastructure.Email;

/// <summary>Production <see cref="IEmailSender"/>: MailKit over SMTP (ADR-0015), configured under <c>Email:Smtp</c>.</summary>
internal sealed class SmtpEmailSender(IOptionsMonitor<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        try
        {
            var mime = EmailMimeMessageFactory.Build(settings.From, message);
            using var client = new SmtpClient();
            await client.ConnectAsync(
                settings.Smtp.Host,
                settings.Smtp.Port,
                settings.Smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                cancellationToken);

            if (!string.IsNullOrEmpty(settings.Smtp.UserName))
            {
                await client.AuthenticateAsync(settings.Smtp.UserName, settings.Smtp.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // No inner exception kept: a caller that logs this (e.g. ILogger.LogError(exception, ...)) must never
            // be able to print the original, unsanitized message (which could echo Email:Smtp:Password) via
            // Exception.ToString()'s inner-exception chain.
            throw new InvalidOperationException(ErrorSanitizer.Sanitize(exception));
        }
    }
}
