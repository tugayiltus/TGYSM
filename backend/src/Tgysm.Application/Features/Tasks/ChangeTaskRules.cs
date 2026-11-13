using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Changes;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tasks;

namespace Tgysm.Application.Features.Tasks;

/// <summary>
/// What a change asks of its tasks (ADR-0032): the phase gate on start and resume, reopen only while the change is
/// open, and the plan rules when an Uygulama task joins or leaves the plan. Callers run inside the change lock.
/// </summary>
public sealed class ChangeTaskRules(IAppDbContext db, ChangeApprovalFlow flow)
{
    /// <summary>Start and resume: the task's phase must be active on its change (409 changes.phase_not_active).</summary>
    public async Task RequireActivePhaseAsync(WorkTask task, CancellationToken cancellationToken)
    {
        if (task is not { ChangeId: not null, Phase: { } phase })
        {
            return;
        }

        var change = await ChangeOfAsync(task, cancellationToken);
        if (!ChangeGate.CanStart(change.Status, phase, change.BackoutStartedAt is not null))
        {
            throw new DomainException(ErrorCodes.ChangePhaseNotActive, $"A {phase} task cannot start while the change is {change.Status}.");
        }
    }

    /// <summary>True for a plain task; for a change task, whether its change is still open.</summary>
    public async Task<bool> ChangeOpenAsync(WorkTask task, CancellationToken cancellationToken) =>
        task.ChangeId is null || (await ChangeOfAsync(task, cancellationToken)).IsOpen;

    /// <summary>
    /// Before an Uygulama task leaves or rejoins the plan: refused once the plan is locked. Returns the change when the
    /// approval has to follow the new plan (<see cref="PlanTasksChangedAsync"/>), otherwise null.
    /// </summary>
    public async Task<ChangeRequest?> BeforePlanTaskChangeAsync(WorkTask task, CancellationToken cancellationToken)
    {
        if (task is not { ChangeId: not null, Phase: ChangePhase.Implementation })
        {
            return null;
        }

        var change = await ChangeOfAsync(task, cancellationToken);
        RequirePlanOpen(change);
        return change.IsOpen ? change : null;
    }

    /// <summary>Saves the task change, then refreshes or voids the approval of the change for the new plan.</summary>
    public async Task PlanTasksChangedAsync(ChangeRequest change, Guid actorUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
        await flow.PlanChangedAsync(change, null, actorUserId, now, cancellationToken);
    }

    /// <summary>A new task joins an open change only; an Uygulama task only while the plan is not locked.</summary>
    public static void RequireCanJoin(ChangeRequest change, ChangePhase phase)
    {
        if (!change.IsOpen)
        {
            throw new DomainException(ErrorCodes.ChangeInvalidTransition, $"A task cannot be added while the change is {change.Status}.");
        }

        if (phase == ChangePhase.Implementation)
        {
            RequirePlanOpen(change);
        }
    }

    private static void RequirePlanOpen(ChangeRequest change)
    {
        if (change.IsPlanLocked)
        {
            throw new DomainException(ErrorCodes.ChangePlanLocked, "The Uygulama tasks are locked once implementation has started.");
        }
    }

    private Task<ChangeRequest> ChangeOfAsync(WorkTask task, CancellationToken cancellationToken) =>
        db.ChangeRequests.SingleAsync(c => c.Id == task.ChangeId, cancellationToken);
}
