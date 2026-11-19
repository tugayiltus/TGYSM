using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Tickets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Changes;

public sealed record LinkedTicketItem(
    string Number, string Title, TicketKind Kind, TicketStatus Status, string TeamName,
    ChangeTicketLinkKind LinkKind, DateTimeOffset LinkedAt, string LinkedByName, bool CanOpen);

public sealed record LinkedChangeItem(
    string Number, string Title, ChangeStatus Status, DateTimeOffset? WindowStart, DateTimeOffset? WindowEnd,
    string OwnerTeamName, ChangeTicketLinkKind LinkKind, DateTimeOffset LinkedAt, bool CanOpen);

public sealed record ListChangeTicketsQuery(string Number);

public sealed record AddChangeTicketCommand(string Number, string TicketNumber, ChangeTicketLinkKind Kind);

public sealed record RemoveChangeTicketCommand(string Number, string TicketNumber);

public sealed record ListTicketChangesQuery(string Number);

internal sealed class AddChangeTicketValidator : AbstractValidator<AddChangeTicketCommand>
{
    public AddChangeTicketValidator()
    {
        RuleFor(c => c.TicketNumber).NotEmpty().MaximumLength(TicketNumber.MaxLength);
        RuleFor(c => c.Kind).IsInEnum();
    }
}

internal static class ChangeTicketLinkQueries
{
    private const string NameSeparator = " ";

    /// <summary>Tickets of a change, newest link first. Every row shows its summary; <c>CanOpen</c> follows the viewer's ticket scope.</summary>
    public static async Task<IReadOnlyList<LinkedTicketItem>> TicketsOfChangeAsync(
        IAppDbContext db, TicketScope tickets, Guid changeId, CancellationToken cancellationToken)
    {
        var viewer = await tickets.ViewerAsync(cancellationToken);
        var openable = tickets.Visible(viewer);
        return await (
            from l in db.ChangeTicketLinks.AsNoTracking()
            join t in db.Tickets on l.TicketId equals t.Id
            where l.ChangeId == changeId
            orderby l.CreatedAt descending, l.Id descending
            select new LinkedTicketItem(
                t.Number, t.Subject, t.Kind, t.Status,
                db.Teams.Where(x => x.Id == t.TeamId).Select(x => x.Name).First(),
                l.Kind, l.CreatedAt,
                db.Users.Where(u => u.Id == l.CreatedById).Select(u => u.FirstName + NameSeparator + u.LastName).First(),
                openable.Any(o => o.Id == t.Id))).ToListAsync(cancellationToken);
    }

    /// <summary>Changes of a ticket, newest link first; <c>CanOpen</c> is full or summary access to the change.</summary>
    public static async Task<IReadOnlyList<LinkedChangeItem>> ChangesOfTicketAsync(
        IAppDbContext db, ChangeScope changes, Guid ticketId, CancellationToken cancellationToken)
    {
        var viewer = await changes.ViewerAsync(cancellationToken);
        var openable = changes.Visible(viewer);
        return await (
            from l in db.ChangeTicketLinks.AsNoTracking()
            join c in db.ChangeRequests on l.ChangeId equals c.Id
            where l.TicketId == ticketId
            orderby l.CreatedAt descending, l.Id descending
            select new LinkedChangeItem(
                c.Number, c.Title, c.Status, c.WindowStart, c.WindowEnd,
                db.Teams.Where(x => x.Id == c.OwnerTeamId).Select(x => x.Name).First(),
                l.Kind, l.CreatedAt,
                openable.Any(o => o.Id == c.Id))).ToListAsync(cancellationToken);
    }
}

/// <summary>Tickets tied to a change. Full or summary access to the change; others get not found.</summary>
public sealed class ListChangeTicketsHandler(IAppDbContext db, ChangeScope scope, TicketScope tickets)
{
    public async Task<IReadOnlyList<LinkedTicketItem>> HandleAsync(ListChangeTicketsQuery query, CancellationToken cancellationToken)
    {
        var (change, _, _) = await scope.LoadAsync(query.Number, cancellationToken);
        return await ChangeTicketLinkQueries.TicketsOfChangeAsync(db, tickets, change.Id, cancellationToken);
    }
}

/// <summary>
/// Changes tied to a ticket, for IT staff who can see the ticket. An employee gets not found, so a requester never
/// learns a change exists (ADR-0033).
/// </summary>
public sealed class ListTicketChangesHandler(IAppDbContext db, TicketScope tickets, ChangeScope changes)
{
    public async Task<IReadOnlyList<LinkedChangeItem>> HandleAsync(ListTicketChangesQuery query, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = await tickets.LoadAsync(query.Number, cancellationToken);
        if (!viewer.SeesInternal)
        {
            throw new NotFoundException(AuditEntityTypes.Ticket);
        }

        return await ChangeTicketLinkQueries.ChangesOfTicketAsync(db, changes, ticket.Id, cancellationToken);
    }
}

/// <summary>
/// Links a ticket of any team to a change. The link changes neither record's version, status nor SLA: it writes one
/// audit row and one timeline event per side, the ticket's event internal-only.
/// </summary>
public sealed class AddChangeTicketHandler(
    IAppDbContext db, IValidator<AddChangeTicketCommand> validator, ChangeScope scope, TicketScope tickets, IAuditLog audit, TimeProvider clock)
{
    public async Task<IReadOnlyList<LinkedTicketItem>> HandleAsync(AddChangeTicketCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        Guid changeId = Guid.Empty;
        await db.RunSerializedAsync(SerializationLocks.ChangeTicketLink, async ct =>
        {
            var (change, viewer) = await scope.LoadForWorkAsync(command.Number, ct);
            changeId = change.Id;
            if (change.Status == ChangeStatus.Cancelled)
            {
                throw new DomainException(ErrorCodes.ChangeTicketLinkCancelled, "A cancelled change takes no new links.");
            }

            var normalized = TicketNumber.Normalize(command.TicketNumber);
            var ticket = await db.Tickets.SingleOrDefaultAsync(t => t.Number == normalized, ct)
                ?? throw new NotFoundException(AuditEntityTypes.Ticket);
            if (await db.ChangeTicketLinks.AnyAsync(l => l.ChangeId == change.Id && l.TicketId == ticket.Id, ct))
            {
                throw new DomainException(ErrorCodes.ChangeTicketLinkExists, LinkExistsMessage);
            }

            var now = clock.GetUtcNow();
            db.ChangeTicketLinks.Add(ChangeTicketLink.Create(change.Id, ticket.Id, ticket.Kind, command.Kind, viewer.UserId, now));
            var data = new { ticketNumber = ticket.Number, changeNumber = change.Number, kind = command.Kind };
            ChangeHistory.Record(db, change, ChangeEventKind.TicketLinked, viewer.UserId, now, data);
            TicketTimeline.Record(db, ticket, TicketEventKind.ChangeLinked, Visibility.Internal, viewer.UserId, now, data);
            audit.Record(AuditActions.ChangeTicketLinked, AuditEntityTypes.Change, change.Id, newValues: data);
            audit.Record(AuditActions.TicketChangeLinked, AuditEntityTypes.Ticket, ticket.Id, newValues: data);
            await db.SaveTranslatingDuplicateAsync(ErrorCodes.ChangeTicketLinkExists, LinkExistsMessage, ct);
            await TicketActivity.TouchAsync(db, ticket.Id, now, ct);
        }, cancellationToken);
        return await ChangeTicketLinkQueries.TicketsOfChangeAsync(db, tickets, changeId, cancellationToken);
    }

    private const string LinkExistsMessage = "The ticket is already linked to this change.";
}

public sealed class RemoveChangeTicketHandler(IAppDbContext db, ChangeScope scope, TicketScope tickets, IAuditLog audit, TimeProvider clock)
{
    public async Task<IReadOnlyList<LinkedTicketItem>> HandleAsync(RemoveChangeTicketCommand command, CancellationToken cancellationToken)
    {
        Guid changeId = Guid.Empty;
        await db.RunSerializedAsync(SerializationLocks.ChangeTicketLink, async ct =>
        {
            var (change, viewer) = await scope.LoadForWorkAsync(command.Number, ct);
            changeId = change.Id;
            var normalized = TicketNumber.Normalize(command.TicketNumber);
            var ticket = await db.Tickets.SingleOrDefaultAsync(t => t.Number == normalized, ct)
                ?? throw new NotFoundException(AuditEntityTypes.Ticket);
            var link = await db.ChangeTicketLinks.SingleOrDefaultAsync(l => l.ChangeId == change.Id && l.TicketId == ticket.Id, ct)
                ?? throw new NotFoundException(AuditEntityTypes.Ticket);
            var now = clock.GetUtcNow();
            db.ChangeTicketLinks.Remove(link);
            var data = new { ticketNumber = ticket.Number, changeNumber = change.Number, kind = link.Kind };
            ChangeHistory.Record(db, change, ChangeEventKind.TicketUnlinked, viewer.UserId, now, data);
            TicketTimeline.Record(db, ticket, TicketEventKind.ChangeUnlinked, Visibility.Internal, viewer.UserId, now, data);
            audit.Record(AuditActions.ChangeTicketUnlinked, AuditEntityTypes.Change, change.Id, oldValues: data);
            audit.Record(AuditActions.TicketChangeUnlinked, AuditEntityTypes.Ticket, ticket.Id, oldValues: data);
            await db.SaveChangesAsync(ct);
            await TicketActivity.TouchAsync(db, ticket.Id, now, ct);
        }, cancellationToken);
        return await ChangeTicketLinkQueries.TicketsOfChangeAsync(db, tickets, changeId, cancellationToken);
    }
}
