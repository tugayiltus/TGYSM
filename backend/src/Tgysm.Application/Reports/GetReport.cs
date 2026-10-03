using Tgysm.Application.Common;
using Tgysm.Domain.Reports;
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

        var now = clock.GetUtcNow();
        var effective = ReportCatalog.EffectiveFilter(query.Report, query.Filter);
        var figures = new List<ReportFigure>();
        foreach (var metric in ReportCatalog.Get(query.Report).OfType<ReportMetric>())
        {
            var pipeline = ReportPipeline.Tickets(scope, source, db, viewer, query.Report, query.Filter, metric.DateBasis, now);
            figures.Add(await metric.ComputeAsync(pipeline.Filtered, pipeline.Context, cancellationToken));
        }

        return new ReportResult(query.Report, effective, ScopeLabelOf(viewer, query.Report), figures);
    }

    /// <summary><see cref="AllScope"/> for admins, <see cref="TeamsScope"/> for IT staff; asset reports always see everything.</summary>
    public static string ScopeLabelOf(ReportViewer viewer, ReportName report) =>
        viewer.Role == UserRole.Admin || report is ReportName.Assets or ReportName.Warranty ? AllScope : TeamsScope;

    /// <summary>Asset and warranty reports are snapshots of all assets, for IT staff and admins alike.</summary>
    private async Task<ReportResult> AssetReport(GetReportQuery query, ReportViewer viewer, CancellationToken cancellationToken)
    {
        var pipeline = ReportPipeline.Assets(scope, db, companyClock, viewer, query.Filter, clock.GetUtcNow());

        var figures = new List<ReportFigure>();
        foreach (var metric in ReportCatalog.Get(query.Report).OfType<AssetReportMetric>())
        {
            figures.Add(await metric.ComputeAsync(pipeline.Filtered, pipeline.Context, cancellationToken));
        }

        return new ReportResult(query.Report, query.Filter, AllScope, figures);
    }
}
