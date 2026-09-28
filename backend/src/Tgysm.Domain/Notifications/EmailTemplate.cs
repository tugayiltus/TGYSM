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
    public const int EventTypeMaxLength = 64;

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

    /// <summary>Sets the wording, enforcing length and `{{name}}` variable validity as a domain invariant (spec §3.4, ADR-0016) — the same <see cref="ErrorCodes"/> the admin API's 400 field errors use.</summary>
    public void Update(string subject, string body, Guid actorId, DateTimeOffset now)
    {
        var trimmedSubject = subject.Trim();
        var trimmedBody = body.Trim();
        EnsureWithinLength(trimmedSubject, SubjectMaxLength);
        EnsureWithinLength(trimmedBody, BodyMaxLength);
        EnsureNoBadVariables(trimmedSubject);
        EnsureNoBadVariables(trimmedBody);

        Subject = trimmedSubject;
        Body = trimmedBody;
        UpdatedBy = actorId;
        UpdatedAt = now;
    }

    private static void EnsureWithinLength(string text, int maxLength)
    {
        if (text.Length > maxLength)
        {
            throw new DomainException(ErrorCodes.TooLong, $"Text exceeds the maximum length of {maxLength}.");
        }
    }

    private void EnsureNoBadVariables(string text)
    {
        var badNames = TemplateRenderer.Validate(text, EventType);
        if (badNames.Count == 0)
        {
            return;
        }

        var code = TemplateVariables.AllVariables.Contains(badNames[0])
            ? ErrorCodes.EmailTemplateVariableNotAllowed
            : ErrorCodes.EmailTemplateUnknownVariable;
        throw new DomainException(code, $"Variable {{{{{badNames[0]}}}}} is not allowed here.");
    }
}
