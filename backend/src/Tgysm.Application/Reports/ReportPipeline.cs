using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports;

/// <summary>The scoped, filtered tickets of one ticket metric with the context it computes in.</summary>
public sealed record TicketPipeline(IQueryable<Ticket> Filtered, ReportContext Context, ReportFilter Effective);

/// <summary>The scoped, filtered assets of an asset report with the context it computes in.</summary>
public sealed record AssetPipeline(IQueryable<Asset> Filtered, AssetReportContext Context);

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

    public static AssetPipeline Assets(ReportScope scope, IAppDbContext db, ICompanyClock companyClock, ReportViewer viewer, ReportFilter filter, DateTimeOffset now) =>
        new(AssetReportSource.Apply(scope.AssetsFor(viewer), filter), AssetReportContext.Create(db, filter, now, companyClock.Zone));
}
