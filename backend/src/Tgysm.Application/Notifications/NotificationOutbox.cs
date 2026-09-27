using System.Text.Json;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Notifications;

namespace Tgysm.Application.Notifications;

internal sealed class NotificationOutbox(IAppDbContext db, TimeProvider clock) : INotificationOutbox
{
    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

    public void Add(
        string eventType,
        Guid? ticketId,
        Guid? userId,
        IReadOnlyDictionary<string, string>? data = null,
        string? protectedSecret = null) =>
        db.OutboxEvents.Add(OutboxEvent.Create(eventType, ticketId, userId, JsonSerializer.Serialize(data ?? Empty), protectedSecret, clock.GetUtcNow()));
}
