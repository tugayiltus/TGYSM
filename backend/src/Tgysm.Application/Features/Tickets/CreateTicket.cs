using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Sla;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tickets;

public sealed record CreateIncidentCommand(string Subject, string Description, Guid? CategoryId, Guid? LocationId);

public sealed record CreateRequestCommand(string Subject, string Description, Guid RequestTypeId, Guid? CategoryId, Guid? LocationId);

internal sealed class CreateIncidentValidator : AbstractValidator<CreateIncidentCommand>
{
    public CreateIncidentValidator()
    {
        RuleFor(c => c.Subject).NotEmpty().MaximumLength(Ticket.SubjectMaxLength);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(Ticket.DescriptionMaxLength);
    }
}

internal sealed class CreateRequestValidator : AbstractValidator<CreateRequestCommand>
{
    public CreateRequestValidator()
    {
        RuleFor(c => c.Subject).NotEmpty().MaximumLength(Ticket.SubjectMaxLength);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(Ticket.DescriptionMaxLength);
        RuleFor(c => c.RequestTypeId).NotEmpty();
    }
}

/// <summary>Shared creation steps: references, intake team, number, events, audit — one transaction.</summary>
public sealed class TicketFactory(
    IAppDbContext db, ITicketNumberGenerator numbers, ICurrentActor actor, IAuditLog audit, INotificationOutbox outbox, TicketScope scope, SlaTracker sla, TimeProvider clock)
{
    private const string InactiveReferenceMessage = "A selected category, location or request type is inactive or does not apply.";

    /// <param name="requesterId">Requester when IT creates on someone's behalf (related request); default: the actor.</param>
    public async Task<TicketDetail> CreateAsync(
        TicketKind kind, Guid? categoryId, Guid? locationId, Func<string, Guid, Guid, DateTimeOffset, Task<Ticket>> build, CancellationToken cancellationToken,
        Guid? requesterId = null)
    {
        var actorId = actor.UserId ?? throw new AuthenticationFailedException();
        var requester = requesterId ?? actorId;
        await EnsureReferencesAsync(kind, categoryId, locationId, cancellationToken);
        var teamId = await db.Teams.Where(t => t.IsDefaultIntake && t.IsActive).Select(t => (Guid?)t.Id).SingleOrDefaultAsync(cancellationToken)
            ?? throw new DomainException(ErrorCodes.TicketNoIntakeTeam, "No default intake team is set.");

        Ticket? ticket = null;
        await db.RunInTransactionAsync(async ct =>
        {
            var now = clock.GetUtcNow();
            var number = TicketNumber.Format(kind, now.Year, await numbers.NextAsync(kind, now.Year, ct));
            ticket = await build(number, requester, teamId, now);
            db.Tickets.Add(ticket);
            await sla.OnCreatedAsync(ticket, ct);
            TicketTimeline.Record(db, ticket, TicketEventKind.Created, Visibility.Public, actorId, now);
            audit.Record(AuditActions.TicketCreated, AuditEntityTypes.Ticket, ticket.Id, newValues: new { ticket.Number, ticket.Kind, ticket.Status });
            outbox.Add(NotificationEventTypes.TicketCreated, ticket.Id, userId: null);
            await db.SaveChangesAsync(ct);
        }, cancellationToken);

        return await TicketQueries.LoadDetailAsync(db, ticket!.Id, await scope.ViewerAsync(cancellationToken), cancellationToken);
    }

    private async Task EnsureReferencesAsync(TicketKind kind, Guid? categoryId, Guid? locationId, CancellationToken cancellationToken)
    {
        if (categoryId is { } category)
        {
            var found = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == category && c.IsActive, cancellationToken);
            if (found is null || !found.AppliesTo(kind))
            {
                throw new DomainException(ErrorCodes.ReferenceInactive, InactiveReferenceMessage);
            }
        }

        if (locationId is { } location && !await db.Locations.AnyAsync(l => l.Id == location && l.IsActive, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ReferenceInactive, InactiveReferenceMessage);
        }
    }
}

public sealed class CreateIncidentHandler(IValidator<CreateIncidentCommand> validator, TicketFactory factory)
{
    public async Task<TicketDetail> HandleAsync(CreateIncidentCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await factory.CreateAsync(TicketKind.Incident, command.CategoryId, command.LocationId, (number, requester, team, now) =>
            Task.FromResult(Ticket.CreateIncident(number, command.Subject, command.Description, command.CategoryId, command.LocationId, requester, team, now)), cancellationToken);
    }
}

/// <summary>A request whose type requires approval also gets a pending approval for the requester's department approver (spec §3.4).</summary>
public sealed class CreateRequestHandler(IAppDbContext db, IValidator<CreateRequestCommand> validator, TicketFactory factory, INotificationOutbox outbox)
{
    public async Task<TicketDetail> HandleAsync(CreateRequestCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var type = await db.RequestTypes.AsNoTracking().SingleOrDefaultAsync(r => r.Id == command.RequestTypeId && r.IsActive, cancellationToken)
            ?? throw new DomainException(ErrorCodes.ReferenceInactive, "The request type is inactive or missing.");

        return await factory.CreateAsync(TicketKind.Request, command.CategoryId, command.LocationId, async (number, requester, team, now) =>
        {
            var ticket = Ticket.CreateRequest(number, command.Subject, command.Description, command.CategoryId, command.LocationId, requester, team, type.Id, type.RequiresApproval, now);
            if (type.RequiresApproval)
            {
                var approver = await ApprovalRules.ApproverForAsync(db, requester, cancellationToken);
                db.Approvals.Add(Approval.Request(ticket.Id, approver, now));
                TicketTimeline.Record(db, ticket, TicketEventKind.ApprovalRequested, Visibility.Public, requester, now);
                outbox.Add(NotificationEventTypes.ApprovalRequested, ticket.Id, approver);
            }

            return ticket;
        }, cancellationToken);
    }
}
