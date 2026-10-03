using Tgysm.Application.Common.Paging;
using Tgysm.Domain.Reports;

namespace Tgysm.Application.Reports;

public sealed record GetDrilldownQuery(ReportName Report, string Metric, string? Bucket, ReportDrillPart Part, ReportFilter Filter, PageRequest Page);

/// <summary>One page of drill-down rows; <c>Kind</c> says which of the two lists is set.</summary>
public sealed record DrilldownResult(DrilldownKind Kind, PagedResult<DrilldownTicketRow>? Tickets, PagedResult<DrilldownAssetRow>? Assets);

/// <summary>The rows behind a figure of <see cref="GetReportHandler"/> (spec §7.3), in the viewer's scope.</summary>
public sealed class GetDrilldownHandler(ReportScope scope, ReportRowSource rows)
{
    public async Task<DrilldownResult> HandleAsync(GetDrilldownQuery query, CancellationToken cancellationToken)
    {
        var viewer = await scope.ViewerAsync(cancellationToken);
        var opened = rows.Open(viewer, query.Report, query.Metric, query.Bucket, query.Part, query.Filter);
        return opened.Kind == DrilldownKind.Assets
            ? new DrilldownResult(opened.Kind, null, await opened.Assets!.ToPagedResultAsync(query.Page, cancellationToken))
            : new DrilldownResult(opened.Kind, await opened.Tickets!.ToPagedResultAsync(query.Page, cancellationToken), null);
    }
}
