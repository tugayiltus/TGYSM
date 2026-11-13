using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;
using Tgysm.Domain.Notifications;

namespace Tgysm.Application.Features.Changes;

public enum ChangeApprovalDecision
{
    Approve,
    Reject,
    Return,
}

public sealed record DecideChangeApprovalCommand(string Number, ChangeApprovalDecision Decision, uint ApprovalVersion, string? Reason);

internal sealed class DecideChangeApprovalValidator : AbstractValidator<DecideChangeApprovalCommand>
{
    public DecideChangeApprovalValidator()
    {
        RuleFor(c => c.Number).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(ChangeApproval.ReasonMaxLength).When(c => c.Decision != ChangeApprovalDecision.Approve);
    }
}

/// <summary>
/// Any eligible admin approves, rejects or returns the pending approval (ADR-0032). The decision is one conditional
/// update on the pending row and its version, so two parallel decisions cannot both win.
/// </summary>
public sealed class DecideChangeApprovalHandler(
    IAppDbContext db, IValidator<DecideChangeApprovalCommand> validator, ChangeScope scope, ChangeWork work,
    IAuditLog audit, INotificationOutbox outbox, TimeProvider clock)
{
    public async Task<ChangeDetail> HandleAsync(DecideChangeApprovalCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var viewer = await scope.ViewerAsync(cancellationToken);
        if (!viewer.IsAdmin)
        {
            throw new ForbiddenException();
        }

        ChangeDetail? detail = null;
        await db.RunSerializedAsync(SerializationLocks.ChangeWork, async ct =>
        {
            var (change, _, _) = await scope.LoadAsync(command.Number, ct);
            change.RequireEligibleApprover(viewer.UserId);
            var now = clock.GetUtcNow();
            var status = StatusOf(command.Decision);
            var reason = command.Decision == ChangeApprovalDecision.Approve ? null : command.Reason!.Trim();
            var pendingId = await db.ChangeApprovals.AsNoTracking()
                .Where(a => a.ChangeId == change.Id && a.Status == ChangeApprovalStatus.Pending)
                .Select(a => (Guid?)a.Id).SingleOrDefaultAsync(ct)
                ?? throw NotPending();
            var decided = await db.ChangeApprovals
                .Where(a => a.Id == pendingId && a.Status == ChangeApprovalStatus.Pending && a.Version == command.ApprovalVersion)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.Status, status)
                    .SetProperty(a => a.Reason, reason)
                    .SetProperty(a => a.DecidedAt, now)
                    .SetProperty(a => a.DecidedById, viewer.UserId), ct);
            if (decided == 0)
            {
                var stillPending = await db.ChangeApprovals.AnyAsync(a => a.Id == pendingId && a.Status == ChangeApprovalStatus.Pending, ct);
                throw stillPending
                    ? new DomainException(ErrorCodes.ConcurrencyConflict, "The approval changed since it was loaded.")
                    : NotPending();
            }

            if (command.Decision == ChangeApprovalDecision.Approve)
            {
                change.Schedule(now);
            }
            else
            {
                change.ReturnToDraft(now);
            }

            ChangeHistory.Record(db, change, EventOf(command.Decision), viewer.UserId, now, new { Reason = reason });
            audit.Record(AuditActions.ChangeApprovalDecided, AuditEntityTypes.Change, change.Id,
                newValues: new { change.Number, Decision = status, change.Status }, reason: reason);
            outbox.AddForSubject(NotificationEventTypes.ChangeApprovalDecided, OutboxSubjectTypes.Change, change.Id, userId: null);
            await db.SaveChangesAsync(ct);
            detail = await work.DetailAsync(change.Id, viewer, ct);
        }, cancellationToken);
        return detail!;
    }

    private static DomainException NotPending() => new(ErrorCodes.ChangeApprovalNotPending, "The change has no pending approval.");

    private static ChangeApprovalStatus StatusOf(ChangeApprovalDecision decision) => decision switch
    {
        ChangeApprovalDecision.Approve => ChangeApprovalStatus.Approved,
        ChangeApprovalDecision.Reject => ChangeApprovalStatus.Rejected,
        _ => ChangeApprovalStatus.Returned,
    };

    private static ChangeEventKind EventOf(ChangeApprovalDecision decision) => decision switch
    {
        ChangeApprovalDecision.Approve => ChangeEventKind.Approved,
        ChangeApprovalDecision.Reject => ChangeEventKind.Rejected,
        _ => ChangeEventKind.Returned,
    };
}
