using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Notifications;

namespace Tgysm.Application.Features.Notifications.Templates;

public sealed record UpdateEmailTemplateCommand(string EventType, uint Version, string Subject, string Body);

internal sealed class UpdateEmailTemplateValidator : AbstractValidator<UpdateEmailTemplateCommand>
{
    public UpdateEmailTemplateValidator()
    {
        RuleFor(c => c.Subject).NotEmpty().MaximumLength(EmailTemplate.SubjectMaxLength);
        RuleFor(c => c.Body).NotEmpty().MaximumLength(EmailTemplate.BodyMaxLength);
    }
}

/// <summary>Updates one event's email wording (ADR-0016); saving an unknown or not-allowed `{{name}}` variable fails with a 400 field error.</summary>
public sealed class UpdateEmailTemplateHandler(
    IAppDbContext db,
    IValidator<UpdateEmailTemplateCommand> validator,
    ICurrentActor actor,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<EmailTemplateItem> HandleAsync(UpdateEmailTemplateCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var template = await db.EmailTemplates.SingleOrDefaultAsync(t => t.EventType == command.EventType, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.EmailTemplate);
        db.SetExpectedVersion(template, command.Version);

        ThrowIfHasBadVariables(nameof(UpdateEmailTemplateCommand.Subject), command.Subject, command.EventType);
        ThrowIfHasBadVariables(nameof(UpdateEmailTemplateCommand.Body), command.Body, command.EventType);

        var before = new { template.Subject, template.Body };
        template.Update(command.Subject, command.Body, actor.UserId ?? throw new AuthenticationFailedException(), clock.GetUtcNow());
        audit.Record(AuditActions.EmailTemplateUpdated, AuditEntityTypes.EmailTemplate, template.Id, before, new { template.Subject, template.Body });
        await db.SaveChangesAsync(cancellationToken);
        return EmailTemplateItem.From(template);
    }

    private static void ThrowIfHasBadVariables(string field, string text, string eventType)
    {
        var badNames = TemplateRenderer.Validate(text, eventType);
        if (badNames.Count == 0)
        {
            return;
        }

        var failures = badNames
            .Select(name => new ValidationFailure(field, $"Variable {{{{{name}}}}} is not allowed here.")
            {
                ErrorCode = TemplateVariables.AllVariables.Contains(name)
                    ? ErrorCodes.EmailTemplateVariableNotAllowed
                    : ErrorCodes.EmailTemplateUnknownVariable,
            })
            .ToList();
        throw new ValidationException(failures);
    }
}
