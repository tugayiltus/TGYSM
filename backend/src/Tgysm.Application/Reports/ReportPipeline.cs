using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports;

/// <summary>The scoped, filtered tickets of one ticket metric with the context it computes in.</summary>
public sealed record TicketPipeline(IQueryable<Ticket> Filtered, ReportContext Context, ReportFilter Effective);

/// <summary>The scoped, filtered assets of an asset report with the context it computes in.</summary>
public sealed record AssetPipeline(IQueryable<Asset> Filtered, AssetReportContext Context);

/// <summary>The scoped, filtered changes of one change metric with the context it computes in.</summary>
public sealed record ChangePipeline(IQueryable<ChangeRequest> Filtered, ReportContext Context);

/// <summary>The scoped, filtered tasks of one task metric with the context it computes in.</summary>
public sealed record TaskPipeline(IQueryable<WorkTask> Filtered, TaskReportContext Context);

/// <summary>
/// The one path from a viewer and a filter to the rows a metric reads: scope, effective filter, filter application, window. The
/// report figures, the drill-down and the export all go through it, so they cannot disagree about which rows are in play.
/// </summary>
public static class ReportPipeline
{
    public static TicketPipeline Tickets(
        ReportScope scope, TicketReportSource source, IAppDbContext db, ReportViewer viewer, ReportName report, ReportFilter filter, ReportDateBasis basis, DateTimeOffset now)
    {
        var effective = ReportCatalog.EffectiveFilter(report, filter);
        var filtered = source.Apply(scope.TicketsFor(viewer), effective, basis);
        return new TicketPipeline(filtered, new ReportContext(db, now, source.Window(effective)), effective);
    }

    public static ChangePipeline Changes(
        ReportScope scope, IAppDbContext db, ICompanyClock companyClock, ReportViewer viewer, ReportFilter filter, ReportDateBasis basis, DateTimeOffset now)
    {
        var window = filter.Window(companyClock.Zone);
        return new ChangePipeline(ChangeReportSource.Apply(scope.ChangesFor(viewer), filter, basis, window), new ReportContext(db, now, window));
    }

    public static TaskPipeline Tasks(
        ReportScope scope, IAppDbContext db, ICompanyClock companyClock, ReportViewer viewer, ReportFilter filter, ReportDateBasis basis, DateTimeOffset now)
    {
        var window = filter.Window(companyClock.Zone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, companyClock.Zone).DateTime);
        return new TaskPipeline(TaskReportSource.Apply(scope.TasksFor(viewer), filter, basis, window), new TaskReportContext(db, now, window, today));
    }

    public static AssetPipeline Assets(ReportScope scope, IAppDbContext db, ICompanyClock companyClock, ReportViewer viewer, ReportFilter filter, DateTimeOffset now) =>
        new(AssetReportSource.Apply(scope.AssetsFor(viewer), filter), AssetReportContext.Create(db, filter, now, companyClock.Zone));
}
