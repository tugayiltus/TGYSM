using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Common.Paging;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tasks;

public enum TaskQueue
{
    Mine,
    Team,
    Unassigned,
    Overdue,
}

public sealed record TaskListItem(
    Guid Id,
    string Number,
    string Title,
    WorkTaskStatus Status,
    Priority Priority,
    DateOnly? DueDate,
    bool IsOverdue,
    Guid TeamId,
    string TeamName,
    string? AssigneeName,
    DateTimeOffset UpdatedAt);

public sealed record ListTasksQuery(TaskQueue? Queue, WorkTaskStatus? Status, Priority? Priority, Guid? TeamId, string? Search, int Page, int PageSize);

/// <summary>Lists tasks in the viewer's scope; queues and filters only narrow it. Employees get not found.</summary>
public sealed class ListTasksHandler(IAppDbContext db, TaskScope scope, ICompanyClock companyClock, TimeProvider clock)
{
    private const string NameSeparator = " ";

    public async Task<PagedResult<TaskListItem>> HandleAsync(ListTasksQuery query, CancellationToken cancellationToken)
    {
        var viewer = await scope.StaffViewerAsync(cancellationToken);
        var today = CompanyToday(companyClock, clock);
        var tasks = scope.Visible(viewer).AsNoTracking();
        var teamIds = viewer.TeamIds;
        tasks = query.Queue switch
        {
            TaskQueue.Mine => tasks.Where(t => t.AssigneeId == viewer.UserId),
            TaskQueue.Team => tasks.Where(t => teamIds.Contains(t.TeamId)),
            TaskQueue.Unassigned => tasks.Where(t => t.AssigneeId == null).Where(OpenTask),
            TaskQueue.Overdue => tasks.Where(OpenTask).Where(t => t.DueDate != null && t.DueDate < today),
            _ => tasks,
        };
        if (query.Status is { } status)
        {
            tasks = tasks.Where(t => t.Status == status);
        }

        if (query.Priority is { } priority)
        {
            tasks = tasks.Where(t => t.Priority == priority);
        }

        if (query.TeamId is { } teamId)
        {
            tasks = tasks.Where(t => t.TeamId == teamId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var number = TaskNumber.Normalize(query.Search);
            var key = TurkishText.ToSearchKey(query.Search);
            tasks = TaskNumber.IsValid(number)
                ? tasks.Where(t => t.Number == number)
                : tasks.Where(t => EF.Functions.Like(t.SearchText, LikePattern.Containing(key), LikePattern.EscapeText));
        }

        return await tasks
            .OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.Id)
            .Select(t => new TaskListItem(
                t.Id, t.Number, t.Title, t.Status, t.Priority, t.DueDate,
                t.Status != WorkTaskStatus.Completed && t.Status != WorkTaskStatus.Cancelled && t.DueDate != null && t.DueDate < today,
                t.TeamId,
                db.Teams.Where(x => x.Id == t.TeamId).Select(x => x.Name).First(),
                db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
                t.UpdatedAt))
            .ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }

    /// <summary>Today's date in the company time zone; due dates are calendar days there.</summary>
    internal static DateOnly CompanyToday(ICompanyClock companyClock, TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), companyClock.Zone).DateTime);

    private static readonly System.Linq.Expressions.Expression<Func<WorkTask, bool>> OpenTask =
        t => t.Status != WorkTaskStatus.Completed && t.Status != WorkTaskStatus.Cancelled;
}
