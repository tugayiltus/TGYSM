using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Sla.Admin;

/// <summary>The whole new policy: a base target for every kind × priority × metric, optional request-type overrides, and the pause rules.</summary>
public sealed record CreatePolicyVersionCommand(IReadOnlyList<PolicyTargetItem> Targets, IReadOnlyList<PauseRuleItem> PauseRules);

internal sealed class CreatePolicyVersionValidator : AbstractValidator<CreatePolicyVersionCommand>
{
    public CreatePolicyVersionValidator()
    {
        RuleFor(c => c.Targets).NotNull();
        RuleFor(c => c.PauseRules).NotNull();
        RuleFor(c => c.Targets).Custom(ValidateTargets).When(c => c.Targets is not null);
        RuleFor(c => c.PauseRules)
            .Must(rules => rules.Select(r => r.HoldReason).Distinct().Count() == rules.Count)
            .WithErrorCode(ErrorCodes.InvalidValue)
            .When(c => c.PauseRules is not null);
    }

    private static void ValidateTargets(IReadOnlyList<PolicyTargetItem> targets, ValidationContext<CreatePolicyVersionCommand> context)
    {
        if (targets.Any(t => t.TargetMinutes is <= 0 or > SlaTarget.MaxTargetMinutes
                || t.WarningPercent is < SlaTarget.MinWarningPercent or > SlaTarget.MaxWarningPercent
                || (t.RequestTypeId is not null && t.Kind != TicketKind.Request)))
        {
            Fail(context, ErrorCodes.InvalidValue);
        }

        var keys = targets.Select(t => (t.Kind, t.Priority, t.RequestTypeId, t.Metric)).ToList();
        if (keys.Distinct().Count() != keys.Count)
        {
            Fail(context, ErrorCodes.InvalidValue);
        }

        var baseCells = targets.Where(t => t.RequestTypeId is null && t.TargetMinutes > 0).Select(t => (t.Kind, t.Priority, t.Metric));
        if (SlaPolicyVersion.FindMissingCells(baseCells).Count > 0)
        {
            Fail(context, ErrorCodes.SlaTargetMissing);
        }
    }

    private static void Fail(ValidationContext<CreatePolicyVersionCommand> context, string code) =>
        context.AddFailure(new ValidationFailure(nameof(CreatePolicyVersionCommand.Targets), code) { ErrorCode = code });
}

/// <summary>
/// Saves the policy as the next version (ADR-0018): the old version keeps its rows and stops being current, the new one
/// becomes current. Serialized under <see cref="SerializationLocks.SlaPolicy"/> so parallel calls number and flip in turn.
/// </summary>
public sealed class CreatePolicyVersionHandler(
    IAppDbContext db,
    IValidator<CreatePolicyVersionCommand> validator,
    ICurrentActor actor,
    IAuditLog audit,
    TimeProvider clock)
{
    public async Task<PolicyDetail> HandleAsync(CreatePolicyVersionCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await EnsureRequestTypesExistAsync(command, cancellationToken);
        var createdBy = actor.UserId ?? throw new AuthenticationFailedException();

        SlaPolicyVersion? created = null;
        await db.RunSerializedAsync(SerializationLocks.SlaPolicy, async ct =>
        {
            var current = await db.SlaPolicyVersions.SingleOrDefaultAsync(v => v.IsCurrent, ct);
            var lastNumber = await db.SlaPolicyVersions.MaxAsync(v => (int?)v.Number, ct) ?? 0;

            // Own save first so the partial unique index never sees two current versions.
            if (current is not null)
            {
                current.Retire();
                await db.SaveChangesAsync(ct);
            }

            created = SlaPolicyVersion.Create(
                lastNumber + 1,
                createdBy,
                clock.GetUtcNow(),
                command.Targets.Select(t => SlaTarget.Draft(t.Kind, t.Priority, t.RequestTypeId, t.Metric, t.TargetMinutes, t.WarningPercent)),
                command.PauseRules.Select(r => SlaPauseRule.Draft(r.HoldReason, r.PausesFirstResponse, r.PausesResolution)));
            db.SlaPolicyVersions.Add(created);
            audit.Record(
                AuditActions.SlaPolicyVersioned,
                AuditEntityTypes.SlaPolicy,
                created.Id,
                oldValues: current is null ? null : new { current.Number },
                newValues: new { created.Number, command.Targets, command.PauseRules });
            await db.SaveChangesAsync(ct);
        }, cancellationToken);

        return PolicyDetail.From(created!);
    }

    private async Task EnsureRequestTypesExistAsync(CreatePolicyVersionCommand command, CancellationToken cancellationToken)
    {
        var typeIds = command.Targets.Where(t => t.RequestTypeId is not null).Select(t => t.RequestTypeId!.Value).Distinct().ToList();
        if (typeIds.Count == 0)
        {
            return;
        }

        var known = await db.RequestTypes.AsNoTracking().CountAsync(t => typeIds.Contains(t.Id), cancellationToken);
        if (known != typeIds.Count)
        {
            throw new ValidationException([new ValidationFailure(nameof(CreatePolicyVersionCommand.Targets), "Unknown request type.") { ErrorCode = ErrorCodes.InvalidValue }]);
        }
    }
}
