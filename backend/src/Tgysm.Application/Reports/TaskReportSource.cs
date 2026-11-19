using Tgysm.Domain.Tasks;

namespace Tgysm.Application.Reports;

/// <summary>
/// Applies the task-related parts of a <see cref="ReportFilter"/>: the team and the day window. The window dates a task by its
/// creation (<see cref="ReportDateBasis.Created"/>), its closing (<see cref="ReportDateBasis.Completed"/>), or not at all
/// (<see cref="ReportDateBasis.None"/>, a snapshot).
/// </summary>
public static class TaskReportSource
{
    public static IQueryable<WorkTask> Apply(IQueryable<WorkTask> tasks, ReportFilter filter, ReportDateBasis basis, ReportWindow window)
    {
        if (basis != ReportDateBasis.None)
        {
            var (from, to) = (window.FromInclusive, window.ToExclusive);
            var byCreation = basis == ReportDateBasis.Created;
            if (from is { } start)
            {
                tasks = byCreation ? tasks.Where(t => t.CreatedAt >= start) : tasks.Where(t => t.ClosedAt >= start);
            }

            if (to is { } end)
            {
                tasks = byCreation ? tasks.Where(t => t.CreatedAt < end) : tasks.Where(t => t.ClosedAt < end);
            }
        }

        return filter.TeamId is { } teamId ? tasks.Where(t => t.TeamId == teamId) : tasks;
    }
}
