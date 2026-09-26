using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Tickets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Approvals;

public sealed record MyApprovalItem(
    Guid Id, uint Version, string TicketNumber, string Subject, string RequesterName, string? RequestTypeName, Guid? ApproverId, DateTimeOffset CreatedAt);

public sealed record DecideApprovalCommand(Guid ApprovalId, uint Version, bool Approve, string? Reason);

public sealed record ReassignApproverCommand(string Number, Guid ApproverId, string Reason);

public sealed record InvalidateApprovalCommand(string Number, uint Version, string Reason);

internal sealed class DecideApprovalValidator : AbstractValidator<DecideApprovalCommand>
{
    public DecideApprovalValidator() =>
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(Approval.ReasonMaxLength).When(c => !c.Approve);
}

internal sealed class ReassignApproverValidator : AbstractValidator<ReassignApproverCommand>
{
    public ReassignApproverValidator()
    {
        RuleFor(c => c.ApproverId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(Approval.ReasonMaxLength);
    }
}

internal sealed class InvalidateApprovalValidator : AbstractValidator<InvalidateApprovalCommand>
{
    public InvalidateApprovalValidator() => RuleFor(c => c.Reason).NotEmpty().MaximumLength(Approval.ReasonMaxLength);
}

/// <summary>Pending approvals assigned to the signed-in admin, plus unassigned ones any admin must reassign.</summary>
public sealed class ListMyApprovalsHandler(IAppDbContext db, ICurrentActor actor)
{
    private const string NameSeparator = " ";

    public async Task<IReadOnlyList<MyApprovalItem>> HandleAsync(CancellationToken cancellationToken)
    {
        var me = actor.UserId ?? throw new AuthenticationFailedException();
        return await (
            from a in db.Approvals.AsNoTracking()
            join t in db.Tickets on a.TicketId equals t.Id
            join u in db.Users on t.RequesterId equals u.Id
            where a.Status == ApprovalStatus.Pending && (a.ApproverId == me || a.ApproverId == null)
            orderby a.CreatedAt
            select new MyApprovalItem(
                a.Id, a.Version, t.Number, t.Subject, u.FirstName + NameSeparator + u.LastName,
                db.RequestTypes.Where(r => r.Id == t.RequestTypeId).Select(r => r.Name).FirstOrDefault(), a.ApproverId, a.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>Approve opens the request; reject cancels it (spec §3.4). Concurrent decisions: one wins, the other gets 409.</summary>
public sealed class DecideApprovalHandler(
    IAppDbContext db, IValidator<DecideApprovalCommand> validator, ICurrentActor actor, IAuditLog audit, TicketScope scope, TimeProvider clock)
{
    public async Task<TicketDetail> HandleAsync(DecideApprovalCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var me = actor.UserId ?? throw new AuthenticationFailedException();
        var approval = await db.Approvals.SingleOrDefaultAsync(a => a.Id == command.ApprovalId, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Approval);
        db.SetExpectedVersion(approval, command.Version);
        var ticket = await db.Tickets.SingleAsync(t => t.Id == approval.TicketId, cancellationToken);
        var now = clock.GetUtcNow();

        if (command.Approve)
        {
            approval.Approve(me, now);
            ticket.ApprovalGranted(now);
        }
        else
        {
            approval.Reject(me, command.Reason!, now);
            ticket.ApprovalRejected(approval.Reason!, now);
        }

        TicketTimeline.Record(db, ticket, TicketEventKind.ApprovalDecided, Visibility.Public, me, now, new { approval.Status, approval.Reason });
        audit.Record(command.Approve ? AuditActions.ApprovalApproved : AuditActions.ApprovalRejected, AuditEntityTypes.Approval, approval.Id,
            newValues: new { ticket.Number, approval.Status }, reason: approval.Reason);
        await db.SaveChangesAsync(cancellationToken);
        return await TicketQueries.LoadDetailAsync(db, ticket.Id, await scope.ViewerAsync(cancellationToken), cancellationToken);
    }
}

/// <summary>Admins give a pending approval to another admin (never the requester), with a reason.</summary>
public sealed class ReassignApproverHandler(IAppDbContext db, IValidator<ReassignApproverCommand> validator, IAuditLog audit, TicketScope scope, TimeProvider clock)
{
    public async Task<TicketDetail> HandleAsync(ReassignApproverCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var (ticket, viewer) = await scope.LoadAsync(command.Number, cancellationToken);
        var approval = await db.Approvals.SingleOrDefaultAsync(a => a.TicketId == ticket.Id && a.Status == ApprovalStatus.Pending, cancellationToken)
            ?? throw new DomainException(ErrorCodes.ApprovalNotPending, "The ticket has no pending approval.");
        if (!await ApprovalRules.IsValidApproverAsync(db, command.ApproverId, ticket.RequesterId, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ApprovalApproverInvalid, "The approver must be an active admin other than the requester.");
        }

        var before = new { approval.ApproverId };
        approval.Reassign(command.ApproverId);
        TicketTimeline.Record(db, ticket, TicketEventKind.ApprovalReassigned, Visibility.Internal, viewer.UserId, clock.GetUtcNow(), new { command.ApproverId });
        audit.Record(AuditActions.ApprovalReassigned, AuditEntityTypes.Approval, approval.Id, before, new { approval.ApproverId }, command.Reason.Trim());
        await db.SaveChangesAsync(cancellationToken);
        return await TicketQueries.LoadDetailAsync(db, ticket.Id, viewer, cancellationToken);
    }
}

/// <summary>After a scope change IT voids the granted approval; a new one is requested and the request waits again.</summary>
public sealed class InvalidateApprovalHandler(IAppDbContext db, IValidator<InvalidateApprovalCommand> validator, TicketWork work, IAuditLog audit)
{
    public async Task<TicketDetail> HandleAsync(InvalidateApprovalCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, requireWorker: true, async (ticket, viewer, now) =>
        {
            var approved = await db.Approvals.Where(a => a.TicketId == ticket.Id && a.Status == ApprovalStatus.Approved)
                .OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(cancellationToken)
                ?? throw new DomainException(ErrorCodes.ApprovalNotPending, "The ticket has no granted approval.");
            approved.Void(command.Reason, now);
            ticket.ReturnToApproval(now);
            var approver = await ApprovalRules.ApproverForAsync(db, ticket.RequesterId, cancellationToken);
            db.Approvals.Add(Approval.Request(ticket.Id, approver, now.AddTicks(1)));
            TicketTimeline.Record(db, ticket, TicketEventKind.ApprovalVoided, Visibility.Public, viewer.UserId, now, new { reason = approved.Reason });
            audit.Record(AuditActions.ApprovalVoided, AuditEntityTypes.Approval, approved.Id, reason: approved.Reason);
        }, cancellationToken);
    }
}
