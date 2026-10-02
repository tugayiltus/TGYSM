using Tgysm.Application.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Reports;

public sealed record GetReportQuery(ReportName Report, ReportFilter Filter);

/// <summary>The figures of one report. <c>ScopeLabel</c> is <c>all</c> or <c>teams</c> (the viewer's teams only).</summary>
public sealed record ReportResult(ReportName Report, ReportFilter Filter, string ScopeLabel, IReadOnlyList<ReportFigure> Figures);

public sealed class GetReportHandler(ReportScope scope, TicketReportSource source, Abstractions.IAppDbContext db, TimeProvider clock, ICompanyClock companyClock)
{
    public const string AllScope = "all";
    public const string TeamsScope = "teams";

    public async Task<ReportResult> HandleAsync(GetReportQuery query, CancellationToken cancellationToken)
    {
        query.Filter.EnsureValid();
        var viewer = await scope.ViewerAsync(cancellationToken);
        if (query.Report is ReportName.Assets or ReportName.Warranty)
        {
            return await AssetReport(query, viewer, cancellationToken);
        }

        var tickets = scope.TicketsFor(viewer);
        var filter = ReportCatalog.EffectiveFilter(query.Report, query.Filter);
        var context = new ReportContext(db, clock.GetUtcNow(), source.Window(filter));

        var figures = new List<ReportFigure>();
        foreach (var metric in ReportCatalog.Get(query.Report).OfType<ReportMetric>())
        {
            figures.Add(await metric.ComputeAsync(source.Apply(tickets, filter, metric.DateBasis), context, cancellationToken));
        }

        return new ReportResult(query.Report, filter, viewer.Role == UserRole.Admin ? AllScope : TeamsScope, figures);
    }

    /// <summary>Asset and warranty reports are snapshots of all assets, for IT staff and admins alike.</summary>
    private async Task<ReportResult> AssetReport(GetReportQuery query, ReportViewer viewer, CancellationToken cancellationToken)
    {
        var assets = AssetReportSource.Apply(scope.AssetsFor(viewer), query.Filter);
        var context = AssetReportContext.Create(db, query.Filter, clock.GetUtcNow(), companyClock.Zone);

        var figures = new List<ReportFigure>();
        foreach (var metric in ReportCatalog.Get(query.Report).OfType<AssetReportMetric>())
        {
            figures.Add(await metric.ComputeAsync(assets, context, cancellationToken));
        }

        return new ReportResult(query.Report, query.Filter, AllScope, figures);
    }
}
