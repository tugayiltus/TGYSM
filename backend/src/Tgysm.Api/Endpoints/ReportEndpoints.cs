using Tgysm.Api.Http;
using Tgysm.Api.Security;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Reports;

namespace Tgysm.Api.Endpoints;

internal static class ReportEndpoints
{
    private const string Tag = "Reports";
    private const string ReportRoute = "/{report}";
    private const string DrilldownRoute = "/{report}/drilldown";
    private const string PartParameter = "part";

    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder api)
    {
        var reports = api.MapGroup(ApiRoutes.Reports).WithTags(Tag).RequireAuthorization(Policies.ItStaffOrAdmin);

        reports.MapGet(ReportRoute, async (string report, HttpContext http, GetReportHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new GetReportQuery(ReportNames.Parse(report), ReportFilterBinder.Bind(http.Request.Query)), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        reports.MapGet(DrilldownRoute, async (
                string report, string metric, string? bucket, string? part, int? page, int? pageSize, HttpContext http, GetDrilldownHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(
                    new GetDrilldownQuery(
                        ReportNames.Parse(report),
                        metric,
                        bucket,
                        QueryParsing.ParseEnum<ReportDrillPart>(part, PartParameter) ?? ReportDrillPart.Numerator,
                        ReportFilterBinder.Bind(http.Request.Query),
                        new PageRequest(page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize)),
                    ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return api;
    }
}
