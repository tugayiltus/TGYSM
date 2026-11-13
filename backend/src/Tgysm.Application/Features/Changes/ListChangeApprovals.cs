using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Changes;

public sealed record ListChangeApprovalsQuery;

public sealed record ChangeApprovalItem(
    Guid Id, string ChangeNumber, string Title, Level Risk, DateTimeOffset? WindowStart, DateTimeOffset? WindowEnd,
    string ImplementationPlanExcerpt, uint Version);

/// <summary>Pending change approvals the caller may decide: an admin who is neither author nor owner. Others get an empty list.</summary>
public sealed class ListChangeApprovalsHandler(IAppDbContext db, ChangeScope scope)
{
    public const int ExcerptLength = 200;

    public async Task<IReadOnlyList<ChangeApprovalItem>> HandleAsync(ListChangeApprovalsQuery query, CancellationToken cancellationToken)
    {
        var viewer = await scope.ViewerAsync(cancellationToken);
        if (!viewer.IsAdmin)
        {
            return [];
        }

        var me = viewer.UserId;
        var rows = await (
            from a in db.ChangeApprovals.AsNoTracking()
            join c in db.ChangeRequests on a.ChangeId equals c.Id
            where a.Status == ChangeApprovalStatus.Pending && c.CreatedById != me && c.OwnerId != me && !db.ChangeApprovalEditors.Any(e => e.ApprovalId == a.Id && e.UserId == me)
            orderby a.CreatedAt
            select new { a.Id, c.Number, c.Title, c.Risk, c.WindowStart, c.WindowEnd, c.ImplementationPlan, a.Version })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => new ChangeApprovalItem(
            r.Id, r.Number, r.Title, r.Risk, r.WindowStart, r.WindowEnd, Excerpt(r.ImplementationPlan), r.Version))];
    }

    private static string Excerpt(string? plan) =>
        plan is null ? string.Empty : plan.Length <= ExcerptLength ? plan : plan[..ExcerptLength];
}
