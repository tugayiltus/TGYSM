using Microsoft.Extensions.Options;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Notifications;

namespace Tgysm.Infrastructure.Email;

/// <summary>
/// Development/test <see cref="IEmailSender"/>: writes one <c>.eml</c> file per message under <c>Email:PickupFolder</c>
/// instead of talking to SMTP (ADR-0015) — no live SMTP dependency for local runs or CI.
/// </summary>
internal sealed class PickupFolderEmailSender(IOptionsMonitor<EmailOptions> options) : IEmailSender
{
    private const string FileExtension = ".eml";

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        try
        {
            if (string.IsNullOrWhiteSpace(settings.PickupFolder))
            {
                throw new InvalidOperationException("Email:PickupFolder must be set when Email:Mode is 'Pickup'.");
            }

            var folder = Path.GetFullPath(settings.PickupFolder);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + FileExtension);

            var mime = EmailMimeMessageFactory.Build(settings.From, message);
            await using var stream = File.Create(path);
            await mime.WriteToAsync(stream, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(ErrorSanitizer.Sanitize(exception, EmailDelivery.LastErrorMaxLength));
        }
    }
}
