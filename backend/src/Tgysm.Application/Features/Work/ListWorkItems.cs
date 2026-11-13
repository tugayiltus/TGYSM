using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Changes;
using Tgysm.Application.Features.Tasks;
using Tgysm.Application.Features.Tickets;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Work;

public enum WorkQueue
{
    AssignedToMe,
    MyTeams,
}

/// <summary>Kind of a work list row: <c>incident</c>, <c>request</c>, <c>task</c> or <c>change</c>.</summary>
public static class WorkItemKinds
{
    public const string Task = "task";

    public const string Change = "change";
}

/// <summary>One row of İş listem: a ticket, a task or a change in one shape (ADR-0031).</summary>
public sealed record WorkItemRow(
    string Kind, string Number, string Title, string Status, Priority? Priority, DateOnly? DueDate, string TeamName, string? AssigneeName, DateTimeOffset UpdatedAt, Level? Risk = null, DateTimeOffset? WindowStart = null);

public sealed record ListWorkItemsQuery(WorkQueue? Queue, int Page, int PageSize);

/// <summary>
/// Open tickets, tasks and changes in one list. The union holds only sort keys, so paging runs in SQL; the page rows are then
/// filled in per kind. Each side uses its own scope (ADR-0013, ADR-0031).
/// </summary>
public sealed class ListWorkItemsHandler(IAppDbContext db, TicketScope tickets, TaskScope tasks, ChangeScope changes)
{
    private const string NameSeparator = " ";
    private const int TicketSource = 0;
    private const int TaskSource = 1;
    private const int ChangeSource = 2;

    // Member initialization, not a constructor: EF Core translates a set operation only over member-init projections.
    private sealed class Key
    {
        public Guid Id { get; init; }

        public int Source { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }
    }

    public async Task<PagedResult<WorkItemRow>> HandleAsync(ListWorkItemsQuery query, CancellationToken cancellationToken)
    {
        var taskViewer = await tasks.StaffViewerAsync(cancellationToken);
        var ticketViewer = await tickets.ViewerAsync(cancellationToken);
        var teamIds = taskViewer.TeamIds;
        var me = taskViewer.UserId;
        var openTickets = tickets.Visible(ticketViewer).AsNoTracking().Where(t => t.Status != TicketStatus.Completed && t.Status != TicketStatus.Cancelled);
        var openTasks = tasks.Visible(taskViewer).AsNoTracking().Where(t => t.Status != WorkTaskStatus.Completed && t.Status != WorkTaskStatus.Cancelled);
        var changeViewer = await changes.ViewerAsync(cancellationToken);
        var openChanges = changes.Visible(changeViewer).AsNoTracking().Where(c => c.Status != ChangeStatus.Closed && c.Status != ChangeStatus.Cancelled);
        (openTickets, openTasks, openChanges) = (query.Queue ?? WorkQueue.AssignedToMe) switch
        {
            WorkQueue.MyTeams => (
                openTickets.Where(t => teamIds.Contains(t.TeamId)),
                openTasks.Where(t => teamIds.Contains(t.TeamId)),
                openChanges.Where(c => teamIds.Contains(c.OwnerTeamId))),
            _ => (
                openTickets.Where(t => t.AssigneeId == me),
                openTasks.Where(t => t.AssigneeId == me),
                openChanges.Where(c => c.OwnerId == me)),
        };
        var keys = openTickets.Select(t => new Key { Id = t.Id, Source = TicketSource, UpdatedAt = t.UpdatedAt })
            .Concat(openTasks.Select(t => new Key { Id = t.Id, Source = TaskSource, UpdatedAt = t.UpdatedAt }))
            .Concat(openChanges.Select(c => new Key { Id = c.Id, Source = ChangeSource, UpdatedAt = c.UpdatedAt }))
            .OrderByDescending(k => k.UpdatedAt).ThenByDescending(k => k.Id);
        var page = await keys.ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
        var rows = await RowsAsync(page.Items, cancellationToken);
        return new PagedResult<WorkItemRow>([.. page.Items.Select(k => rows[k.Id])], page.Page, page.PageSize, page.TotalCount);
    }

    private async Task<Dictionary<Guid, WorkItemRow>> RowsAsync(IReadOnlyList<Key> keys, CancellationToken cancellationToken)
    {
        var ticketIds = keys.Where(k => k.Source == TicketSource).Select(k => k.Id).ToList();
        var taskIds = keys.Where(k => k.Source == TaskSource).Select(k => k.Id).ToList();
        var changeIds = keys.Where(k => k.Source == ChangeSource).Select(k => k.Id).ToList();
        var ticketRows = await db.Tickets.AsNoTracking().Where(t => ticketIds.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                t.Kind,
                t.Number,
                t.Subject,
                t.Status,
                t.Priority,
                t.UpdatedAt,
                Team = db.Teams.Where(x => x.Id == t.TeamId).Select(x => x.Name).First(),
                Assignee = db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        var taskRows = await db.WorkTasks.AsNoTracking().Where(t => taskIds.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                t.Number,
                t.Title,
                t.Status,
                t.Priority,
                t.DueDate,
                t.UpdatedAt,
                Team = db.Teams.Where(x => x.Id == t.TeamId).Select(x => x.Name).First(),
                Assignee = db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        var changeRows = await db.ChangeRequests.AsNoTracking().Where(c => changeIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.Number,
                c.Title,
                c.Status,
                c.Risk,
                c.WindowStart,
                c.UpdatedAt,
                Team = db.Teams.Where(x => x.Id == c.OwnerTeamId).Select(x => x.Name).First(),
                Owner = db.Users.Where(u => u.Id == c.OwnerId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        var rows = ticketRows.ToDictionary(
            t => t.Id,
            t => new WorkItemRow(Snake(t.Kind), t.Number, t.Subject, Snake(t.Status), t.Priority, null, t.Team, t.Assignee, t.UpdatedAt));
        foreach (var t in taskRows)
        {
            rows[t.Id] = new WorkItemRow(WorkItemKinds.Task, t.Number, t.Title, Snake(t.Status), t.Priority, t.DueDate, t.Team, t.Assignee, t.UpdatedAt);
        }

        foreach (var c in changeRows)
        {
            rows[c.Id] = new WorkItemRow(WorkItemKinds.Change, c.Number, c.Title, Snake(c.Status), null, null, c.Team, c.Owner, c.UpdatedAt, c.Risk, c.WindowStart);
        }

        return rows;
    }

    private static string Snake<TEnum>(TEnum value)
        where TEnum : struct, Enum => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
}
