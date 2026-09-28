using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Notifications;

namespace Tgysm.Application.Notifications;

/// <summary>
/// Builds the `{{name}}` values for one recipient's rendering of one <see cref="OutboxEvent"/> (spec §3.4),
/// restricted to <see cref="TemplateVariables.AllowedFor"/> that event — a value computed for a variable the event
/// does not allow is simply never set, so <see cref="TemplateRenderer.Render"/> cannot leak it even if a future
/// template edit mistakenly referenced it. Account-link variables (<c>actionUrl</c>, <c>expiresAt</c>) are
/// deliberately never set here: they depend on the event's <c>protected_secret</c>, decrypted only at send time.
/// </summary>
public sealed class NotificationContextLoader(IAppDbContext db, ICompanyClock clock, IOptionsMonitor<AppOptions> options)
{
    private const string TicketUrlPathFormat = "/tickets/{0}";

    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(OutboxEvent outboxEvent, Recipient recipient, CancellationToken cancellationToken)
    {
        var allowed = TemplateVariables.AllowedFor(outboxEvent.EventType);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        void Set(string name, string? value)
        {
            if (value is not null && allowed.Contains(name))
            {
                values[name] = value;
            }
        }

        Set(TemplateVariables.RecipientName, recipient.FullName);

        if (outboxEvent.TicketId is { } ticketId)
        {
            var ticket = await db.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId, cancellationToken);
            Set(TemplateVariables.TicketNumber, ticket.Number);
            Set(TemplateVariables.Subject, ticket.Subject);
            Set(TemplateVariables.Status, ticket.Status.ToString());
            Set(TemplateVariables.TicketUrl, TicketUrl(ticket.Number));
            Set(TemplateVariables.Resolution, ticket.Resolution);
            Set(TemplateVariables.CompletedAt, ticket.CompletedAt is { } completedAt ? clock.Format(completedAt) : null);
        }

        // sla_warning/sla_breached: metric and due time come from the event's own payload (ids and reason codes
        // only, per OutboxEvent.Payload) once the SLA monitor (a later task) starts writing them.
        Set(TemplateVariables.SlaMetric, PayloadString(outboxEvent, "metric"));
        var dueAt = PayloadDate(outboxEvent, "dueAt");
        Set(TemplateVariables.DueAt, dueAt is { } due ? clock.Format(due) : null);

        return values;
    }

    private string TicketUrl(string number) =>
        options.CurrentValue.PublicBaseUrl.TrimEnd('/') + string.Format(System.Globalization.CultureInfo.InvariantCulture, TicketUrlPathFormat, number);

    private static string? PayloadString(OutboxEvent outboxEvent, string key)
    {
        using var payload = JsonDocument.Parse(outboxEvent.Payload);
        return payload.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static DateTimeOffset? PayloadDate(OutboxEvent outboxEvent, string key)
    {
        var raw = PayloadString(outboxEvent, key);
        return raw is not null && DateTimeOffset.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }
}
