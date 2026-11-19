using Tgysm.Domain.Changes;

namespace Tgysm.Application.Reports;

/// <summary>
/// Applies the change-related parts of a <see cref="ReportFilter"/>: the owner team and the day window. The window dates a change by
/// its creation (<see cref="ReportDateBasis.Created"/>) or its closing or cancellation (<see cref="ReportDateBasis.Completed"/>).
/// </summary>
public static class ChangeReportSource
{
    public static IQueryable<ChangeRequest> Apply(IQueryable<ChangeRequest> changes, ReportFilter filter, ReportDateBasis basis, ReportWindow window)
    {
        var (from, to) = (window.FromInclusive, window.ToExclusive);
        var byCreation = basis == ReportDateBasis.Created;
        if (from is { } start)
        {
            changes = byCreation ? changes.Where(c => c.CreatedAt >= start) : changes.Where(c => c.ClosedAt >= start);
        }

        if (to is { } end)
        {
            changes = byCreation ? changes.Where(c => c.CreatedAt < end) : changes.Where(c => c.ClosedAt < end);
        }

        return filter.TeamId is { } teamId ? changes.Where(c => c.OwnerTeamId == teamId) : changes;
    }
}
