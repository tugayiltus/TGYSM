using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Tasks;

namespace Tgysm.Application.Reports.Metrics;

/// <summary>
/// The figures of the task report (ADR-0031). <see cref="Overdue"/> is a snapshot of open tasks and ignores the period; the others
/// date tasks by creation, or by closing for <see cref="CompletionTime"/>.
/// </summary>
public static class TaskMetrics
{
    public const string Overdue = "overdue";
    public const string ByStatus = "by_status";
    public const string CompletionTime = "completion_time";
    public const string ByTeam = "by_team";

    private static readonly WorkTaskStatus[] StatusOrder =
        [WorkTaskStatus.Open, WorkTaskStatus.InProgress, WorkTaskStatus.OnHold, WorkTaskStatus.Completed, WorkTaskStatus.Cancelled];

    private static readonly Expression<Func<WorkTask, bool>> IsOpen =
        t => t.Status != WorkTaskStatus.Completed && t.Status != WorkTaskStatus.Cancelled;

    internal static IReadOnlyList<IReportMetric> All { get; } = [new OverdueMetric(), new StatusMetric(), new CompletionTimeMetric(), new TeamMetric()];

    private static ReportFigure CountFigure(string key, long count) => new(key, MetricShape.Count, count, null, null, null, MetricKeys.UnitTasks);

    /// <summary>Open tasks whose due date lies before today in the company zone.</summary>
    private sealed class OverdueMetric : TaskReportMetric
    {
        public override string Key => Overdue;

        public override ReportName Report => ReportName.Tasks;

        public override ReportDateBasis DateBasis => ReportDateBasis.None;

        public override MetricShape Shape => MetricShape.Count;

        public override IQueryable<WorkTask> Rows(IQueryable<WorkTask> filtered, TaskReportContext context)
        {
            var today = context.Today;
            return filtered.Where(IsOpen).Where(t => t.DueDate < today);
        }

        public override async Task<ReportFigure> ComputeAsync(IQueryable<WorkTask> filtered, TaskReportContext context, CancellationToken cancellationToken) =>
            CountFigure(Key, await Rows(filtered, context).LongCountAsync(cancellationToken));
    }

    /// <summary>Tasks created in the period by status; every status is a bucket, empty ones included.</summary>
    private sealed class StatusMetric : TaskReportMetric
    {
        public override string Key => ByStatus;

        public override ReportName Report => ReportName.Tasks;

        public override ReportDateBasis DateBasis => ReportDateBasis.Created;

        public override MetricShape Shape => MetricShape.Distribution;

        public override IQueryable<WorkTask> Rows(IQueryable<WorkTask> filtered, TaskReportContext context) => filtered;

        public override IQueryable<WorkTask> BucketRows(IQueryable<WorkTask> filtered, TaskReportContext context, string bucket)
        {
            var status = StatusOrder.Where(s => ReportFilter.EnumName(s) == bucket).Select(s => (WorkTaskStatus?)s).FirstOrDefault()
                ?? throw new NotSupportedException($"Unknown bucket '{bucket}'.");
            return filtered.Where(t => t.Status == status);
        }

        public override async Task<ReportFigure> ComputeAsync(IQueryable<WorkTask> filtered, TaskReportContext context, CancellationToken cancellationToken)
        {
            var counts = await filtered.GroupBy(t => t.Status)
                .Select(g => new { g.Key, Count = g.LongCount() })
                .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
            var buckets = StatusOrder.Select(s => new ReportBucket(ReportFilter.EnumName(s), null, counts.GetValueOrDefault(s))).ToList();
            return new ReportFigure(Key, Shape, buckets.Sum(b => b.Count), null, null, buckets, MetricKeys.UnitTasks);
        }
    }

    /// <summary>
    /// Creation-to-closing time of tasks completed in the period (cancelled tasks do not count). Same shape as the other duration figures:
    /// the value is the average in seconds, the denominator the number of tasks, and the <see cref="MetricKeys.Median"/> bucket the median.
    /// </summary>
    private sealed class CompletionTimeMetric : TaskReportMetric
    {
        public override string Key => CompletionTime;

        public override ReportName Report => ReportName.Tasks;

        public override ReportDateBasis DateBasis => ReportDateBasis.Completed;

        public override MetricShape Shape => MetricShape.Duration;

        public override IQueryable<WorkTask> Rows(IQueryable<WorkTask> filtered, TaskReportContext context) =>
            filtered.Where(t => t.Status == WorkTaskStatus.Completed);

        public override async Task<ReportFigure> ComputeAsync(IQueryable<WorkTask> filtered, TaskReportContext context, CancellationToken cancellationToken)
        {
            var seconds = Rows(filtered, context).Select(t => (long)(t.ClosedAt!.Value - t.CreatedAt).TotalSeconds);
            var (average, count) = await DurationStats.AverageAsync(seconds, cancellationToken);
            var median = await DurationStats.MedianAsync(seconds, cancellationToken);
            ReportBucket[]? buckets = median is { } middle ? [new ReportBucket(MetricKeys.Median, null, (long)Math.Round(middle))] : null;
            return new ReportFigure(Key, Shape, average, null, count, buckets, MetricKeys.UnitSeconds);
        }
    }

    /// <summary>Tasks created in the period by team; the bucket key is the team id and the label its name.</summary>
    private sealed class TeamMetric : TaskReportMetric
    {
        public override string Key => ByTeam;

        public override ReportName Report => ReportName.Tasks;

        public override ReportDateBasis DateBasis => ReportDateBasis.Created;

        public override MetricShape Shape => MetricShape.Distribution;

        public override IQueryable<WorkTask> Rows(IQueryable<WorkTask> filtered, TaskReportContext context) => filtered;

        public override IQueryable<WorkTask> BucketRows(IQueryable<WorkTask> filtered, TaskReportContext context, string bucket)
        {
            var teamId = Guid.TryParse(bucket, out var id) ? id : throw new NotSupportedException($"Unknown bucket '{bucket}'.");
            return filtered.Where(t => t.TeamId == teamId);
        }

        public override async Task<ReportFigure> ComputeAsync(IQueryable<WorkTask> filtered, TaskReportContext context, CancellationToken cancellationToken)
        {
            var counts = await filtered.GroupBy(t => t.TeamId)
                .Select(g => new { TeamId = g.Key, Count = g.LongCount() })
                .ToListAsync(cancellationToken);
            var ids = counts.Select(c => c.TeamId).ToList();
            var names = await context.Db.Teams.Where(t => ids.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
            var buckets = counts
                .Select(c => new ReportBucket(c.TeamId.ToString(), names.GetValueOrDefault(c.TeamId), c.Count))
                .OrderByDescending(b => b.Count).ThenBy(b => b.Label, StringComparer.Ordinal)
                .ToList();
            return new ReportFigure(Key, Shape, buckets.Sum(b => b.Count), null, null, buckets, MetricKeys.UnitTasks);
        }
    }
}
