using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Sla;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tickets;

public sealed record StartTicketCommand(string Number, uint Version);

public sealed record HoldTicketCommand(string Number, uint Version, HoldReason? Reason, string? Note);

public sealed record ResumeTicketCommand(string Number, uint Version);

public sealed record CompleteTicketCommand(string Number, uint Version, string Resolution, TicketOutcome? Outcome, string? RootCause);

public sealed record CancelTicketCommand(string Number, uint Version, string Reason);

public sealed record ChangePriorityCommand(string Number, uint Version, Level? Impact, Level? Urgency, string Reason);

internal sealed class HoldTicketValidator : AbstractValidator<HoldTicketCommand>
{
    public HoldTicketValidator()
    {
        RuleFor(c => c.Reason).NotNull().IsInEnum();
        RuleFor(c => c.Note).MaximumLength(Ticket.ReasonMaxLength);
    }
}

internal sealed class CompleteTicketValidator : AbstractValidator<CompleteTicketCommand>
{
    public CompleteTicketValidator()
    {
        RuleFor(c => c.Resolution).NotEmpty().MaximumLength(Ticket.TextMaxLength);
        RuleFor(c => c.RootCause).MaximumLength(Ticket.TextMaxLength);
        RuleFor(c => c.Outcome).IsInEnum();
    }
}

internal sealed class CancelTicketValidator : AbstractValidator<CancelTicketCommand>
{
    public CancelTicketValidator() => RuleFor(c => c.Reason).NotEmpty().MaximumLength(Ticket.ReasonMaxLength);
}

internal sealed class ChangePriorityValidator : AbstractValidator<ChangePriorityCommand>
{
    public ChangePriorityValidator()
    {
        RuleFor(c => c.Impact).NotNull().IsInEnum();
        RuleFor(c => c.Urgency).NotNull().IsInEnum();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(Ticket.ReasonMaxLength);
    }
}

/// <summary>Records a public status event and an audit row for a status change.</summary>
internal static class StatusChange
{
    public static void Record(IAppDbContext db, IAuditLog audit, Ticket ticket, TicketStatus from, Guid actorUserId, DateTimeOffset now, string? reason = null)
    {
        TicketTimeline.Record(db, ticket, TicketEventKind.StatusChanged, Visibility.Public, actorUserId, now, new { from, to = ticket.Status, ticket.HoldReason });
        audit.Record(AuditActions.TicketStatusChanged, AuditEntityTypes.Ticket, ticket.Id, new { Status = from }, new { ticket.Status, ticket.HoldReason }, reason);
    }
}

public sealed class StartTicketHandler(IAppDbContext db, TicketWork work, IAuditLog audit)
{
    public Task<TicketDetail> HandleAsync(StartTicketCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, requireWorker: true, (ticket, viewer, now) =>
        {
            var from = ticket.Status;
            ticket.Start(now);
            StatusChange.Record(db, audit, ticket, from, viewer.UserId, now);
            return Task.CompletedTask;
        }, cancellationToken);
}

public sealed class HoldTicketHandler(IAppDbContext db, IValidator<HoldTicketCommand> validator, TicketWork work, IAuditLog audit, SlaTracker sla)
{
    public async Task<TicketDetail> HandleAsync(HoldTicketCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, requireWorker: true, async (ticket, viewer, now) =>
        {
            var from = ticket.Status;
            ticket.Hold(command.Reason!.Value, command.Note, now);
            StatusChange.Record(db, audit, ticket, from, viewer.UserId, now, ticket.HoldNote);
            await sla.OnHoldAsync(ticket, command.Reason.Value, cancellationToken);
        }, cancellationToken);
    }
}

public sealed class ResumeTicketHandler(IAppDbContext db, TicketWork work, IAuditLog audit, SlaTracker sla)
{
    public Task<TicketDetail> HandleAsync(ResumeTicketCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, requireWorker: true, async (ticket, viewer, now) =>
        {
            var from = ticket.Status;
            ticket.Resume(now);
            StatusChange.Record(db, audit, ticket, from, viewer.UserId, now);
            await sla.OnResumeAsync(ticket, cancellationToken);
        }, cancellationToken);
}

/// <summary>Completing closes the ticket directly (no confirmation step, spec §1 criterion 4).</summary>
public sealed class CompleteTicketHandler(IAppDbContext db, IValidator<CompleteTicketCommand> validator, TicketWork work, IAuditLog audit, INotificationOutbox outbox, SlaTracker sla)
{
    public async Task<TicketDetail> HandleAsync(CompleteTicketCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, requireWorker: true, async (ticket, viewer, now) =>
        {
            var hasRelatedRequest = await db.TicketRelations.AnyAsync(r => r.IncidentId == ticket.Id, cancellationToken);
            var latest = await TicketQueries.LatestApprovalAsync(db, ticket.Id, cancellationToken);
            ticket.Complete(command.Resolution, command.Outcome, hasRelatedRequest, latest?.Status == ApprovalStatus.Approved, now, command.RootCause);
            TicketTimeline.Record(db, ticket, TicketEventKind.Completed, Visibility.Public, viewer.UserId, now, new { ticket.Resolution, ticket.Outcome });
            audit.Record(AuditActions.TicketCompleted, AuditEntityTypes.Ticket, ticket.Id, newValues: new { ticket.Outcome });
            outbox.Add(NotificationEventTypes.TicketCompleted, ticket.Id, userId: null);
            await sla.OnCompletedAsync(ticket, cancellationToken);
        }, cancellationToken);
    }
}

public sealed class CancelTicketHandler(IAppDbContext db, IValidator<CancelTicketCommand> validator, TicketWork work, IAuditLog audit, SlaTracker sla)
{
    public async Task<TicketDetail> HandleAsync(CancelTicketCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, requireWorker: true, async (ticket, viewer, now) =>
        {
            ticket.Cancel(command.Reason, now);
            // A pending approval has nothing left to decide.
            var pending = await db.Approvals.SingleOrDefaultAsync(a => a.TicketId == ticket.Id && a.Status == ApprovalStatus.Pending, cancellationToken);
            pending?.Withdraw(now);
            TicketTimeline.Record(db, ticket, TicketEventKind.Cancelled, Visibility.Public, viewer.UserId, now, new { ticket.CancelReason });
            audit.Record(AuditActions.TicketCancelled, AuditEntityTypes.Ticket, ticket.Id, reason: ticket.CancelReason);
            await sla.OnCancelledAsync(ticket, cancellationToken);
        }, cancellationToken);
    }
}

public sealed class ChangePriorityHandler(IAppDbContext db, IValidator<ChangePriorityCommand> validator, TicketWork work, IAuditLog audit, SlaTracker sla)
{
    public async Task<TicketDetail> HandleAsync(ChangePriorityCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, requireWorker: true, async (ticket, viewer, now) =>
        {
            var before = new { ticket.Impact, ticket.Urgency, ticket.Priority };
            var (from, to) = ticket.ChangePriority(command.Impact!.Value, command.Urgency!.Value, now);
            TicketTimeline.Record(db, ticket, TicketEventKind.PriorityChanged, Visibility.Internal, viewer.UserId, now, new { from, to, reason = command.Reason.Trim() });
            audit.Record(AuditActions.TicketPriorityChanged, AuditEntityTypes.Ticket, ticket.Id, before, new { ticket.Impact, ticket.Urgency, ticket.Priority }, command.Reason.Trim());
            await sla.OnPriorityChangedAsync(ticket, cancellationToken);
        }, cancellationToken);
    }
}
