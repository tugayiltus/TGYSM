using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Notifications;

namespace Tgysm.Application.Features.Notifications.Templates;

public sealed record EmailTemplateItem(Guid Id, string EventType, string Subject, string Body, Guid UpdatedBy, DateTimeOffset UpdatedAt, uint Version, IReadOnlyList<string> AllowedVariables)
{
    /// <summary><see cref="AllowedVariables"/> is the event's <see cref="TemplateVariables"/> set, ordered, so the editor can list what may be used.</summary>
    public static EmailTemplateItem From(EmailTemplate template) =>
        new(template.Id, template.EventType, template.Subject, template.Body, template.UpdatedBy, template.UpdatedAt, template.Version,
            TemplateVariables.AllowedFor(template.EventType).Order(StringComparer.Ordinal).ToList());
}

/// <summary>Lists every event's email template (one row per <see cref="NotificationEventTypes"/> constant, seeded by migration).</summary>
public sealed class ListEmailTemplatesHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<EmailTemplateItem>> HandleAsync(CancellationToken cancellationToken) =>
        (await db.EmailTemplates.AsNoTracking()
            .OrderBy(t => t.EventType)
            .ToListAsync(cancellationToken))
        .Select(EmailTemplateItem.From)
        .ToList();
}
