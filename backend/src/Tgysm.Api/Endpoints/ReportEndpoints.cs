using Tgysm.Api.Http;
using Tgysm.Api.Security;
using Tgysm.Application.Reports;

namespace Tgysm.Api.Endpoints;

internal static class ReportEndpoints
{
    private const string Tag = "Reports";
    private const string ReportRoute = "/{report}";

    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder api)
    {
        var reports = api.MapGroup(ApiRoutes.Reports).WithTags(Tag).RequireAuthorization(Policies.ItStaffOrAdmin);

        reports.MapGet(ReportRoute, async (string report, HttpContext http, GetReportHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new GetReportQuery(ReportNames.Parse(report), ReportFilterBinder.Bind(http.Request.Query)), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return api;
    }
}
