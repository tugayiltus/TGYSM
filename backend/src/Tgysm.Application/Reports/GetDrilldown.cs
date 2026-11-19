using Tgysm.Application.Common.Paging;
using Tgysm.Domain.Reports;

namespace Tgysm.Application.Reports;

public sealed record GetDrilldownQuery(ReportName Report, string Metric, string? Bucket, ReportDrillPart Part, ReportFilter Filter, PageRequest Page);

/// <summary>One page of drill-down rows; <c>Kind</c> says which of the lists is set.</summary>
public sealed record DrilldownResult(
    DrilldownKind Kind, PagedResult<DrilldownTicketRow>? Tickets, PagedResult<DrilldownAssetRow>? Assets, PagedResult<DrilldownChangeRow>? Changes,
    PagedResult<DrilldownTaskRow>? Tasks);

/// <summary>The rows behind a figure of <see cref="GetReportHandler"/> (spec §7.3), in the viewer's scope.</summary>
public sealed class GetDrilldownHandler(ReportScope scope, ReportRowSource rows)
{
    public async Task<DrilldownResult> HandleAsync(GetDrilldownQuery query, CancellationToken cancellationToken)
    {
        var viewer = await scope.ViewerAsync(cancellationToken);
        var opened = rows.Open(viewer, query.Report, query.Metric, query.Bucket, query.Part, query.Filter);
        return opened.Kind switch
        {
            DrilldownKind.Assets => new DrilldownResult(opened.Kind, null, await opened.Assets!.ToPagedResultAsync(query.Page, cancellationToken), null, null),
            DrilldownKind.Changes => new DrilldownResult(opened.Kind, null, null, await opened.Changes!.ToPagedResultAsync(query.Page, cancellationToken), null),
            DrilldownKind.Tasks => new DrilldownResult(opened.Kind, null, null, null, await opened.Tasks!.ToPagedResultAsync(query.Page, cancellationToken)),
            _ => new DrilldownResult(opened.Kind, await opened.Tickets!.ToPagedResultAsync(query.Page, cancellationToken), null, null, null),
        };
    }
}
