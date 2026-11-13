using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Changes;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Users;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Notifications;

/// <summary>
/// Resolves who receives one <see cref="OutboxEvent"/>'s email, from current data (spec §3.2 step 2, §3.3), not
/// from the event's payload snapshot: a ticket's assignee or team can have changed since the event was written.
/// Inactive users never receive email. The acting user is excluded only for the two events spec §3.3 lists a
/// "not sent to" rule for (<c>ticket_assigned</c>, <c>public_reply</c>) — every other event's actor is either
/// unrelated to its recipient or, for <c>ticket_created</c>, is the requester who should get their own
/// confirmation, so excluding them everywhere would silently swallow that email.
/// </summary>
public sealed class RecipientResolver(IAppDbContext db)
{
    private static readonly IReadOnlySet<string> ActorExcludedEvents = new HashSet<string>(StringComparer.Ordinal)
    {
        NotificationEventTypes.TicketAssigned, NotificationEventTypes.PublicReply, NotificationEventTypes.TaskAssigned,
        NotificationEventTypes.ChangeBackoutStarted, NotificationEventTypes.ChangeApprovalDecided,
    };

    public async Task<IReadOnlyList<Recipient>> ResolveAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken)
    {
        var ticket = outboxEvent.TicketId is { } ticketId
            ? await db.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId, cancellationToken)
            : null;

        var candidateIds = await CandidateIdsAsync(outboxEvent, ticket, cancellationToken);
        if (candidateIds.Count == 0)
        {
            return [];
        }

        var query = db.Users.AsNoTracking().Where(u => candidateIds.Contains(u.Id) && u.IsActive);
        if (ActorExcludedEvents.Contains(outboxEvent.EventType) && ActorIdOf(outboxEvent) is { } actorId)
        {
            // A plain `!= actorId` with a nullable parameter would translate to SQL's three-valued `<>`, which is
            // false (row excluded) for every row once the parameter is NULL — hence only adding this filter when
            // there is an actor to exclude, rather than always comparing against a possibly-null value.
            query = query.Where(u => u.Id != actorId);
        }

        var rows = await query
            .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(u => new Recipient(u.Id, u.Email, $"{u.FirstName} {u.LastName}"))];
    }

    private async Task<IReadOnlyList<Guid>> CandidateIdsAsync(OutboxEvent outboxEvent, Ticket? ticket, CancellationToken cancellationToken)
    {
        switch (outboxEvent.EventType)
        {
            case NotificationEventTypes.TicketCreated:
            case NotificationEventTypes.ApprovalDecided:
            case NotificationEventTypes.TicketCompleted:
                // user-visible resolution only: TicketCompleted is emitted for both incidents and requests
                // whenever Complete() succeeds, which is always a user-visible resolution (spec §3.3).
                return [ticket!.RequesterId];

            case NotificationEventTypes.ApprovalRequested:
                return outboxEvent.UserId is { } approver ? [approver] : [];

            case NotificationEventTypes.TicketAssigned:
                return outboxEvent.UserId is { } assignee ? [assignee] : await AssigneeOrTeamIdsAsync(ticket!, cancellationToken);

            case NotificationEventTypes.PublicReply:
                return outboxEvent.UserId == ticket!.RequesterId
                    ? await AssigneeOrTeamIdsAsync(ticket, cancellationToken)
                    : [ticket.RequesterId];

            case NotificationEventTypes.SlaWarning:
            case NotificationEventTypes.SlaBreached:
                return await AssigneeOrTeamIdsAsync(ticket!, cancellationToken);

            case NotificationEventTypes.TaskAssigned:
                return outboxEvent.UserId is { } taskAssignee ? [taskAssignee] : await TaskTeamIdsAsync(outboxEvent, cancellationToken);

            case NotificationEventTypes.ChangeApprovalRequested:
                return await ChangeApproverIdsAsync(outboxEvent, cancellationToken);

            case NotificationEventTypes.ChangeApprovalDecided:
                return await ChangeAuthorAndOwnerIdsAsync(outboxEvent, cancellationToken);

            case NotificationEventTypes.ChangeBackoutStarted:
                return await BackoutWorkerIdsAsync(outboxEvent, cancellationToken);

            case NotificationEventTypes.AccountActivation:
            case NotificationEventTypes.PasswordReset:
                return outboxEvent.UserId is { } user ? [user] : [];

            default:
                throw new ArgumentOutOfRangeException(nameof(outboxEvent), outboxEvent.EventType, "Unknown notification event type.");
        }
    }

    /// <summary>The admin named in the payload (a voided approval), else every active admin except the change's author and owner.</summary>
    private async Task<IReadOnlyList<Guid>> ChangeApproverIdsAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken)
    {
        if (ApproverIdOf(outboxEvent) is { } approverId)
        {
            return [approverId];
        }

        var change = await db.ChangeRequests.AsNoTracking()
            .Where(c => c.Id == outboxEvent.SubjectId)
            .Select(c => new { c.CreatedById, c.OwnerId })
            .SingleAsync(cancellationToken);
        var editors = ChangeApprovalFlow.PendingEditorIds(db, outboxEvent.SubjectId.GetValueOrDefault());
        return await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Admin && u.IsActive && u.Id != change.CreatedById && u.Id != change.OwnerId && !editors.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> ChangeAuthorAndOwnerIdsAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken)
    {
        var change = await db.ChangeRequests.AsNoTracking()
            .Where(c => c.Id == outboxEvent.SubjectId)
            .Select(c => new { c.CreatedById, c.OwnerId })
            .SingleAsync(cancellationToken);
        return [.. new[] { change.CreatedById, change.OwnerId }.Distinct()];
    }

    /// <summary>Assignees of the change's open Geri dönüş tasks; an unassigned task goes to its team's members.</summary>
    private async Task<IReadOnlyList<Guid>> BackoutWorkerIdsAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken)
    {
        var tasks = await db.WorkTasks.AsNoTracking()
            .Where(t => t.ChangeId == outboxEvent.SubjectId && t.Phase == ChangePhase.Backout
                && t.Status != WorkTaskStatus.Completed && t.Status != WorkTaskStatus.Cancelled)
            .Select(t => new { t.AssigneeId, t.TeamId })
            .ToListAsync(cancellationToken);
        var ids = tasks.Where(t => t.AssigneeId is not null).Select(t => t.AssigneeId!.Value).ToHashSet();
        foreach (var teamId in tasks.Where(t => t.AssigneeId is null).Select(t => t.TeamId).Distinct())
        {
            ids.UnionWith(await TeamMemberIdsAsync(teamId, cancellationToken));
        }

        return [.. ids];
    }

    private async Task<IReadOnlyList<Guid>> AssigneeOrTeamIdsAsync(Ticket ticket, CancellationToken cancellationToken) =>
        ticket.AssigneeId is { } assignee ? [assignee] : await TeamMemberIdsAsync(ticket.TeamId, cancellationToken);

    /// <summary>A task event without an assignee goes to the active members of the task's current team.</summary>
    private async Task<IReadOnlyList<Guid>> TaskTeamIdsAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken)
    {
        var teamId = await db.WorkTasks.Where(t => t.Id == outboxEvent.SubjectId).Select(t => t.TeamId).SingleAsync(cancellationToken);
        return await TeamMemberIdsAsync(teamId, cancellationToken);
    }

    private Task<List<Guid>> TeamMemberIdsAsync(Guid teamId, CancellationToken cancellationToken) =>
        (from m in db.TeamMemberships
         join u in db.Users on m.UserId equals u.Id
         where m.TeamId == teamId && u.IsActive
         select m.UserId).ToListAsync(cancellationToken);

    private static Guid? ApproverIdOf(OutboxEvent outboxEvent)
    {
        using var payload = JsonDocument.Parse(outboxEvent.Payload);
        return payload.RootElement.TryGetProperty(OutboxEvent.ApproverIdKey, out var value) ? value.GetGuid() : null;
    }

    private static Guid? ActorIdOf(OutboxEvent outboxEvent)
    {
        using var payload = JsonDocument.Parse(outboxEvent.Payload);
        return payload.RootElement.TryGetProperty(OutboxEvent.ActorIdKey, out var value) ? value.GetGuid() : null;
    }
}
