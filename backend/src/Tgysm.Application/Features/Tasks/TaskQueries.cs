using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Tickets;

namespace Tgysm.Application.Features.Tasks;

internal static class TaskQueries
{
    private const string NameSeparator = " ";

    public static async Task<TaskDetail> LoadDetailAsync(
        IAppDbContext db, TicketScope tickets, ICompanyClock companyClock, TimeProvider clock, Guid taskId, TaskViewer viewer, CancellationToken cancellationToken)
    {
        var t = await db.WorkTasks.AsNoTracking().SingleAsync(x => x.Id == taskId, cancellationToken);
        var userIds = new[] { t.CreatedById, t.AssigneeId ?? t.CreatedById };
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FirstName + NameSeparator + u.LastName, cancellationToken);
        var team = await db.Teams.Where(x => x.Id == t.TeamId).Select(x => x.Name).SingleAsync(cancellationToken);
        var link = t.TicketId is { } ticketId ? await LinkAsync(db, tickets, ticketId, cancellationToken) : null;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), companyClock.Zone).DateTime);

        return new TaskDetail(
            t.Id, t.Number, t.Title, t.Description, t.Status, t.HoldReason, t.HoldNote, t.Priority, t.DueDate, t.IsOverdue(today),
            t.TeamId, team, t.AssigneeId, t.AssigneeId is { } assignee ? names.GetValueOrDefault(assignee) : null,
            t.CreatedById, names[t.CreatedById], link, t.ResultNote, t.CancelReason, t.CreatedAt, t.UpdatedAt, t.ClosedAt,
            viewer.CanWork(t), t.Version);
    }

    private static async Task<LinkedTicket> LinkAsync(IAppDbContext db, TicketScope tickets, Guid ticketId, CancellationToken cancellationToken)
    {
        var ticketViewer = await tickets.ViewerAsync(cancellationToken);
        var ticket = await db.Tickets.AsNoTracking().SingleAsync(x => x.Id == ticketId, cancellationToken);
        return ticketViewer.CanSee(ticket)
            ? new LinkedTicket(true, ticket.Number, ticket.Kind, ticket.Subject, ticket.Status)
            : new LinkedTicket(false, null, null, null, null);
    }
}
