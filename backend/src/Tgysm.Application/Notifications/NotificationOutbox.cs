using System.Collections.Frozen;
using System.Text.Json;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Notifications;

namespace Tgysm.Application.Notifications;

/// <summary>
/// Writes every event with the current actor's id under <see cref="OutboxEvent.ActorIdKey"/> (spec §3.3: some
/// events are "not sent to the actor/author"; the dispatcher needs that id to apply the rule). Single source of
/// truth: handlers never pass it themselves.
/// </summary>
internal sealed class NotificationOutbox(IAppDbContext db, ICurrentActor actor, TimeProvider clock) : INotificationOutbox
{
    private static readonly FrozenDictionary<string, string> Empty = FrozenDictionary<string, string>.Empty;

    public void Add(
        string eventType,
        Guid? ticketId,
        Guid? userId,
        IReadOnlyDictionary<string, string>? data = null,
        string? protectedSecret = null) =>
        db.OutboxEvents.Add(OutboxEvent.Create(eventType, ticketId, userId, PayloadOf(data), protectedSecret, clock.GetUtcNow()));

    public void AddForSubject(string eventType, string subjectType, Guid subjectId, Guid? userId, IReadOnlyDictionary<string, string>? data = null) =>
        db.OutboxEvents.Add(OutboxEvent.CreateForSubject(eventType, subjectType, subjectId, userId, PayloadOf(data), clock.GetUtcNow()));

    private string PayloadOf(IReadOnlyDictionary<string, string>? data)
    {
        var payload = new Dictionary<string, string>(data ?? Empty);
        if (actor.UserId is { } actorId)
        {
            payload[OutboxEvent.ActorIdKey] = actorId.ToString();
        }

        return JsonSerializer.Serialize(payload);
    }
}
