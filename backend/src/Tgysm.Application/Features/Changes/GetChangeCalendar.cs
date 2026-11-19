using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Common;
using Tgysm.Application.Reports;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Changes;

public static class ChangeCalendarLimits
{
    /// <summary>Longest calendar range in company days, both ends included; it keeps one request to about two months.</summary>
    public const int MaxDays = 62;
}

public sealed record ChangeCalendarItem(
    string Number, string Title, ChangeStatus Status, Level Risk, DateTimeOffset WindowStart, DateTimeOffset WindowEnd, bool HasConflict);

/// <param name="From">First company day of the range; null when missing or not a date, which is rejected.</param>
/// <param name="To">Last company day of the range, included.</param>
public sealed record GetChangeCalendarQuery(DateOnly? From, DateOnly? To);

/// <summary>
/// Changes in the viewer's scope whose window touches the range, drafts and cancelled ones left out. Only summary fields
/// are returned. Employees get not found.
/// </summary>
public sealed class GetChangeCalendarHandler(ChangeScope scope, ChangeConflicts conflicts, ICompanyClock companyClock)
{
    public async Task<IReadOnlyList<ChangeCalendarItem>> HandleAsync(GetChangeCalendarQuery query, CancellationToken cancellationToken)
    {
        var viewer = await scope.StaffViewerAsync(cancellationToken);
        if (query.From is not { } from || query.To is not { } to || to < from || to.DayNumber - from.DayNumber + 1 > ChangeCalendarLimits.MaxDays)
        {
            throw new DomainException(ErrorCodes.ChangeCalendarRange, "The range is missing, reversed or too long.");
        }

        var start = ReportFilter.StartOfDay(from, companyClock.Zone);
        var end = ReportFilter.StartOfDay(to.AddDays(1), companyClock.Zone);
        var rows = await scope.Visible(viewer).AsNoTracking()
            .Where(c => c.Status != ChangeStatus.Draft && c.Status != ChangeStatus.Cancelled
                && c.WindowStart != null && c.WindowEnd != null && c.WindowStart < end && c.WindowEnd > start)
            .OrderBy(c => c.WindowStart).ThenBy(c => c.Number)
            .Select(c => new { c.Id, c.Number, c.Title, c.Status, c.Risk, WindowStart = c.WindowStart!.Value, WindowEnd = c.WindowEnd!.Value })
            .ToListAsync(cancellationToken);
        var found = await conflicts.FindAsync([.. rows.Select(r => r.Id)], viewer, cancellationToken);
        return [.. rows.Select(r => new ChangeCalendarItem(r.Number, r.Title, r.Status, r.Risk, r.WindowStart, r.WindowEnd, found[r.Id].Count > 0))];
    }
}
