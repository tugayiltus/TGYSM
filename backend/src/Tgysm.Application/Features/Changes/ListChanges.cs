using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Common.Paging;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Changes;

public enum ChangeQueue
{
    Mine,
    Team,
    PendingApproval,
    Open,
}

/// <summary>List row. Outcome and owner names show only for viewers with full access; summary viewers get the working basics.</summary>
public sealed record ChangeListItem(
    Guid Id,
    string Number,
    string Title,
    ChangeStatus Status,
    ChangeOutcome? Outcome,
    Level Risk,
    DateTimeOffset? WindowStart,
    DateTimeOffset? WindowEnd,
    string? OwnerTeamName,
    string? OwnerName,
    DateTimeOffset UpdatedAt);

public sealed record ListChangesQuery(ChangeQueue? Queue, ChangeStatus? Status, Level? Risk, string? Search, int Page, int PageSize);

/// <summary>Lists changes in the viewer's scope; queues and filters only narrow it. Employees get not found.</summary>
public sealed class ListChangesHandler(IAppDbContext db, ChangeScope scope)
{
    private const string NameSeparator = " ";

    public async Task<PagedResult<ChangeListItem>> HandleAsync(ListChangesQuery query, CancellationToken cancellationToken)
    {
        var viewer = await scope.StaffViewerAsync(cancellationToken);
        var changes = scope.Visible(viewer).AsNoTracking();
        var teamIds = viewer.TeamIds;
        var userId = viewer.UserId;
        var isAdmin = viewer.IsAdmin;
        changes = query.Queue switch
        {
            ChangeQueue.Mine => changes.Where(c => c.OwnerId == userId),
            ChangeQueue.Team => changes.Where(c => teamIds.Contains(c.OwnerTeamId)),
            ChangeQueue.PendingApproval => changes.Where(c => c.Status == ChangeStatus.PendingApproval),
            ChangeQueue.Open => changes.Where(c => c.Status != ChangeStatus.Closed && c.Status != ChangeStatus.Cancelled),
            _ => changes,
        };
        if (query.Status is { } status)
        {
            changes = changes.Where(c => c.Status == status);
        }

        if (query.Risk is { } risk)
        {
            changes = changes.Where(c => c.Risk == risk);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var number = ChangeNumber.Normalize(query.Search);
            var key = TurkishText.ToSearchKey(query.Search);
            changes = ChangeNumber.IsValid(number)
                ? changes.Where(c => c.Number == number)
                : changes.Where(c => EF.Functions.Like(c.SearchText, LikePattern.Containing(key), LikePattern.EscapeText));
        }

        return await changes
            .OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id)
            .Select(c => new ChangeListItem(
                c.Id, c.Number, c.Title, c.Status, isAdmin || teamIds.Contains(c.OwnerTeamId) ? c.Outcome : null, c.Risk, c.WindowStart, c.WindowEnd,
                isAdmin || teamIds.Contains(c.OwnerTeamId) ? db.Teams.Where(x => x.Id == c.OwnerTeamId).Select(x => x.Name).First() : null,
                isAdmin || teamIds.Contains(c.OwnerTeamId)
                    ? db.Users.Where(u => u.Id == c.OwnerId).Select(u => u.FirstName + NameSeparator + u.LastName).First()
                    : null,
                c.UpdatedAt))
            .ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
