using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Tickets;

public sealed record ClaimTicketCommand(string Number, uint Version);

public sealed record TransferTicketCommand(string Number, uint Version, Guid TeamId, Guid? AssigneeId, TransferReason Reason, string? Note);

internal sealed class TransferTicketValidator : AbstractValidator<TransferTicketCommand>
{
    public TransferTicketValidator()
    {
        RuleFor(c => c.TeamId).NotEmpty();
        RuleFor(c => c.Reason).IsInEnum();
        RuleFor(c => c.Note).MaximumLength(TicketAssignment.NoteMaxLength);
        RuleFor(c => c.Note).NotEmpty().When(c => c.Reason == TransferReason.Other);
    }
}

/// <summary>IT staff take an unassigned ticket of their team (spec §3.5); a concurrent claim gets 409.</summary>
public sealed class ClaimTicketHandler(IAppDbContext db, TicketWork work, IAuditLog audit)
{
    public Task<TicketDetail> HandleAsync(ClaimTicketCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, requireWorker: true, (ticket, viewer, now) =>
        {
            if (ticket.AssigneeId is not null)
            {
                throw new DomainException(ErrorCodes.TicketAlreadyAssigned, "The ticket already has an assignee.");
            }

            db.TicketAssignments.Add(ticket.Assign(ticket.TeamId, viewer.UserId, now, viewer.UserId));
            TicketTimeline.Record(db, ticket, TicketEventKind.Assigned, Visibility.Public, viewer.UserId, now, new { assigneeId = viewer.UserId });
            audit.Record(AuditActions.TicketAssigned, AuditEntityTypes.Ticket, ticket.Id, newValues: new { AssigneeId = viewer.UserId });
            return Task.CompletedTask;
        }, cancellationToken);
}

/// <summary>Moves team and/or assignee with a reason; the assignee must be an active member of the target team.</summary>
public sealed class TransferTicketHandler(IAppDbContext db, IValidator<TransferTicketCommand> validator, TicketWork work, IAuditLog audit)
{
    public async Task<TicketDetail> HandleAsync(TransferTicketCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, requireWorker: true, async (ticket, viewer, now) =>
        {
            if (!await db.Teams.AnyAsync(t => t.Id == command.TeamId && t.IsActive, cancellationToken))
            {
                throw new DomainException(ErrorCodes.ReferenceInactive, "The target team is inactive or missing.");
            }

            if (command.AssigneeId is { } assignee && !await (
                    from m in db.TeamMemberships
                    join u in db.Users on m.UserId equals u.Id
                    where m.TeamId == command.TeamId && u.Id == assignee && u.IsActive && u.Role != UserRole.Employee
                    select m).AnyAsync(cancellationToken))
            {
                throw new DomainException(ErrorCodes.TicketAssigneeNotInTeam, "The assignee is not an active member of the target team.");
            }

            var before = new { ticket.TeamId, ticket.AssigneeId };
            db.TicketAssignments.Add(ticket.Assign(command.TeamId, command.AssigneeId, now, viewer.UserId, command.Reason, command.Note));
            TicketTimeline.Record(db, ticket, TicketEventKind.Transferred, Visibility.Internal, viewer.UserId, now,
                new { command.TeamId, command.AssigneeId, command.Reason, command.Note });
            audit.Record(AuditActions.TicketTransferred, AuditEntityTypes.Ticket, ticket.Id, before, new { command.TeamId, command.AssigneeId, command.Reason });
        }, cancellationToken);
    }
}
