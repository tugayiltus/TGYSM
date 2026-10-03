using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports.Metrics;

/// <summary>The figures of the operations report (spec §7.2). Every figure is a distribution whose buckets drill down to their tickets.</summary>
public static class OperationsMetrics
{
    public const string TeamLoad = "team_load";
    public const string AssigneeLoad = "assignee_load";
    public const string ByCategory = "by_category";
    public const string ByLocation = "by_location";
    public const string ByDepartment = "by_department";
    public const string HoldReasons = "hold_reasons";
    public const string TeamCompleted = "team_completed";

    /// <summary>Bucket key of tickets without an assignee.</summary>
    public const string Unassigned = "unassigned";

    /// <summary>Bucket key of tickets without a category, location or requester department.</summary>
    public const string None = "none";

    internal static IReadOnlyList<IReportMetric> All { get; } =
    [
        new TeamLoadMetric(),
        new AssigneeLoadMetric(),
        new CategoryMetric(),
        new LocationMetric(),
        new DepartmentMetric(),
        new HoldReasonMetric(),
        new TeamCompletedMetric(),
    ];

    /// <summary>Open tickets now by current team (no date window).</summary>
    private sealed class TeamLoadMetric : IdGroupedMetric
    {
        public override string Key => TeamLoad;
        public override ReportDateBasis DateBasis => ReportDateBasis.None;
        protected override string NullKey => None;

        protected override IQueryable<Ticket> Population(IQueryable<Ticket> filtered, ReportContext context) => filtered.Where(TicketRules.Open);

        protected override Expression<Func<Ticket, Guid?>> KeyOf(ReportContext context) => t => t.TeamId;

        protected override Task<Dictionary<Guid, string>> NamesAsync(ReportContext context, List<Guid> ids, CancellationToken cancellationToken) =>
            context.Db.Teams.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    }

    /// <summary>Open tickets now by assignee; no assignee is the <see cref="Unassigned"/> bucket.</summary>
    private sealed class AssigneeLoadMetric : IdGroupedMetric
    {
        public override string Key => AssigneeLoad;
        public override ReportDateBasis DateBasis => ReportDateBasis.None;
        protected override string NullKey => Unassigned;

        protected override IQueryable<Ticket> Population(IQueryable<Ticket> filtered, ReportContext context) => filtered.Where(TicketRules.Open);

        protected override Expression<Func<Ticket, Guid?>> KeyOf(ReportContext context) => t => t.AssigneeId;

        protected override Task<Dictionary<Guid, string>> NamesAsync(ReportContext context, List<Guid> ids, CancellationToken cancellationToken) =>
            context.Db.Users.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FirstName + " " + x.LastName, cancellationToken);
    }

    /// <summary>Tickets created in the window by category.</summary>
    private sealed class CategoryMetric : IdGroupedMetric
    {
        public override string Key => ByCategory;
        public override ReportDateBasis DateBasis => ReportDateBasis.Created;
        protected override string NullKey => None;

        protected override IQueryable<Ticket> Population(IQueryable<Ticket> filtered, ReportContext context) => filtered;

        protected override Expression<Func<Ticket, Guid?>> KeyOf(ReportContext context) => t => t.CategoryId;

        protected override Task<Dictionary<Guid, string>> NamesAsync(ReportContext context, List<Guid> ids, CancellationToken cancellationToken) =>
            context.Db.Categories.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    }

    /// <summary>Tickets created in the window by location.</summary>
    private sealed class LocationMetric : IdGroupedMetric
    {
        public override string Key => ByLocation;
        public override ReportDateBasis DateBasis => ReportDateBasis.Created;
        protected override string NullKey => None;

        protected override IQueryable<Ticket> Population(IQueryable<Ticket> filtered, ReportContext context) => filtered;

        protected override Expression<Func<Ticket, Guid?>> KeyOf(ReportContext context) => t => t.LocationId;

        protected override Task<Dictionary<Guid, string>> NamesAsync(ReportContext context, List<Guid> ids, CancellationToken cancellationToken) =>
            context.Db.Locations.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    }

    /// <summary>Tickets created in the window by the requester's department.</summary>
    private sealed class DepartmentMetric : IdGroupedMetric
    {
        public override string Key => ByDepartment;
        public override ReportDateBasis DateBasis => ReportDateBasis.Created;
        protected override string NullKey => None;

        protected override IQueryable<Ticket> Population(IQueryable<Ticket> filtered, ReportContext context) => filtered;

        protected override Expression<Func<Ticket, Guid?>> KeyOf(ReportContext context)
        {
            var users = context.Db.Users;
            return t => users.Where(u => u.Id == t.RequesterId).Select(u => u.DepartmentId).FirstOrDefault();
        }

        protected override Task<Dictionary<Guid, string>> NamesAsync(ReportContext context, List<Guid> ids, CancellationToken cancellationToken) =>
            context.Db.Departments.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    }

    /// <summary>Tickets completed in the window by the team that held them at completion: the target of the last assignment up to then, else the current team.</summary>
    private sealed class TeamCompletedMetric : IdGroupedMetric
    {
        public override string Key => TeamCompleted;
        public override ReportDateBasis DateBasis => ReportDateBasis.Completed;
        protected override string NullKey => None;

        protected override IQueryable<Ticket> Population(IQueryable<Ticket> filtered, ReportContext context) => filtered.Where(TicketRules.Completed);

        protected override Expression<Func<Ticket, Guid?>> KeyOf(ReportContext context)
        {
            var assignments = context.Db.TicketAssignments;
            return t => assignments
                .Where(a => a.TicketId == t.Id && a.OccurredAt <= t.CompletedAt)
                .OrderByDescending(a => a.OccurredAt)
                .ThenByDescending(a => a.Id)
                .Select(a => (Guid?)a.ToTeamId)
                .FirstOrDefault() ?? t.TeamId;
        }

        protected override Task<Dictionary<Guid, string>> NamesAsync(ReportContext context, List<Guid> ids, CancellationToken cancellationToken) =>
            context.Db.Teams.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
    }

    /// <summary>Tickets on hold now by hold reason; the bucket key is the snake_case reason, the label stays null (the UI translates it).</summary>
    private sealed class HoldReasonMetric : GroupedMetric<HoldReason?>
    {
        public override string Key => HoldReasons;
        public override ReportDateBasis DateBasis => ReportDateBasis.None;

        protected override IQueryable<Ticket> Population(IQueryable<Ticket> filtered, ReportContext context) =>
            filtered.Where(t => t.Status == TicketStatus.OnHold);

        protected override Expression<Func<Ticket, HoldReason?>> KeyOf(ReportContext context) => t => t.HoldReason;

        protected override string BucketKey(HoldReason? key) => key is { } reason ? ReportFilter.EnumName(reason) : None;

        protected override HoldReason? Parse(string bucket) =>
            bucket == None ? null : Enum.GetValues<HoldReason>().Cast<HoldReason?>().FirstOrDefault(r => ReportFilter.EnumName(r!.Value) == bucket)
                ?? throw new NotSupportedException($"Unknown bucket '{bucket}'.");

        protected override Task<Dictionary<string, string>> LabelsAsync(ReportContext context, List<HoldReason?> keys, CancellationToken cancellationToken) =>
            Task.FromResult(new Dictionary<string, string>());
    }
}

/// <summary>A distribution that groups a ticket population by one key; buckets drill down through <see cref="BucketRows"/> with the same predicate.</summary>
internal abstract class GroupedMetric<TKey> : ReportMetric
{
    public override ReportName Report => ReportName.Operations;

    public override MetricShape Shape => MetricShape.Distribution;

    protected abstract IQueryable<Ticket> Population(IQueryable<Ticket> filtered, ReportContext context);

    protected abstract Expression<Func<Ticket, TKey>> KeyOf(ReportContext context);

    protected abstract string BucketKey(TKey key);

    protected abstract TKey Parse(string bucket);

    /// <summary>Display names by bucket key; keys without an entry get a null label.</summary>
    protected abstract Task<Dictionary<string, string>> LabelsAsync(ReportContext context, List<TKey> keys, CancellationToken cancellationToken);

    public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context) => Population(filtered, context);

    public override IQueryable<Ticket> BucketRows(IQueryable<Ticket> filtered, ReportContext context, string bucket)
    {
        var key = KeyOf(context);
        var equal = Expression.Equal(key.Body, Expression.Constant(Parse(bucket), typeof(TKey)));
        return Population(filtered, context).Where(Expression.Lambda<Func<Ticket, bool>>(equal, key.Parameters));
    }

    public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken)
    {
        var groups = await Population(filtered, context)
            .GroupBy(KeyOf(context))
            .Select(g => new { g.Key, Count = g.LongCount() })
            .ToListAsync(cancellationToken);
        var labels = await LabelsAsync(context, [.. groups.Select(g => g.Key)], cancellationToken);
        var buckets = groups
            .Select(g => (Key: BucketKey(g.Key), g.Count))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new ReportBucket(g.Key, labels.GetValueOrDefault(g.Key), g.Count))
            .ToList();
        return new ReportFigure(Key, Shape, buckets.Sum(b => b.Count), null, null, buckets, MetricKeys.UnitTickets);
    }
}

/// <summary>Grouping by an optional id: the bucket key is the id, or <see cref="NullKey"/> for none; labels are names read from the database.</summary>
internal abstract class IdGroupedMetric : GroupedMetric<Guid?>
{
    protected abstract string NullKey { get; }

    protected abstract Task<Dictionary<Guid, string>> NamesAsync(ReportContext context, List<Guid> ids, CancellationToken cancellationToken);

    protected override string BucketKey(Guid? key) => key?.ToString() ?? NullKey;

    protected override Guid? Parse(string bucket) =>
        bucket == NullKey ? null : Guid.TryParse(bucket, out var id) ? id : throw new NotSupportedException($"Unknown bucket '{bucket}'.");

    protected override async Task<Dictionary<string, string>> LabelsAsync(ReportContext context, List<Guid?> keys, CancellationToken cancellationToken)
    {
        var ids = keys.OfType<Guid>().ToList();
        var names = await NamesAsync(context, ids, cancellationToken);
        return names.ToDictionary(n => n.Key.ToString(), n => n.Value);
    }
}
