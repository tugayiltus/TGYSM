using Tgysm.Domain.Common;

namespace Tgysm.Domain.Notifications;

/// <summary>
/// Editable wording for one notification event type (ADR-0016). Plain text only; seeded with Turkish defaults by
/// migration and edited in `/admin`. <see cref="EventType"/> is one of <see cref="NotificationEventTypes"/>.
/// </summary>
public sealed class EmailTemplate : Entity
{
    public const int SubjectMaxLength = 200;
    public const int BodyMaxLength = 10_000;

    private EmailTemplate()
    {
    }

    public string EventType { get; private init; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public Guid UpdatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static EmailTemplate Create(string eventType, string subject, string body, Guid updatedBy, DateTimeOffset now)
    {
        var template = new EmailTemplate { EventType = eventType };
        template.Update(subject, body, updatedBy, now);
        return template;
    }

    public void Update(string subject, string body, Guid actorId, DateTimeOffset now)
    {
        Subject = subject.Trim();
        Body = body.Trim();
        UpdatedBy = actorId;
        UpdatedAt = now;
    }
}
