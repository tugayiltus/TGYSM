using Tgysm.Api.Http;
using Tgysm.Api.Security;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Reports;
using Tgysm.Domain.Reports;

namespace Tgysm.Api.Endpoints;

internal static class ReportEndpoints
{
    private const string Tag = "Reports";
    private const string ReportRoute = "/{report}";
    private const string DrilldownRoute = "/{report}/drilldown";
    private const string ExportRoute = "/{report}/export";
    private const string ExportJobsRoute = "/exports";
    private const string ExportDownloadRoute = "/exports/{id:guid}/download";
    private const string PartParameter = "part";
    private const string NoSniffHeader = "X-Content-Type-Options";
    private const string NoSniff = "nosniff";

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

        reports.MapGet(ExportRoute, async (
                string report, string metric, string? bucket, string? part, HttpContext http, ExportReportHandler h, CancellationToken ct) =>
            {
                var file = await h.HandleAsync(
                    new ExportReportQuery(
                        ReportNames.Parse(report),
                        metric,
                        bucket,
                        QueryParsing.ParseEnum<ReportDrillPart>(part, PartParameter) ?? ReportDrillPart.Numerator,
                        ReportFilterBinder.Bind(http.Request.Query)),
                    ct);
                return TypedResults.Stream(output => file.WriteAsync(output, ct), file.ContentType, file.FileName);
            })
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Background export (spec §7.3): the same query parameters as the synchronous export, so the client can fall back to a job
        // with the URL it already has.
        reports.MapPost(ExportJobsRoute, async (
                string report, string metric, string? bucket, string? part, HttpContext http, CreateExportJobHandler h, CancellationToken ct) =>
                TypedResults.Accepted((string?)null, await h.HandleAsync(
                    new CreateExportJobCommand(
                        ReportNames.Parse(report),
                        metric,
                        bucket,
                        QueryParsing.ParseEnum<ReportDrillPart>(part, PartParameter) ?? ReportDrillPart.Numerator,
                        ReportFilterBinder.Bind(http.Request.Query)),
                    ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        reports.MapGet(ExportJobsRoute, async (int? page, int? pageSize, ListMyExportsHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new ListMyExportsQuery(new PageRequest(page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize)), ct)))
            .ProducesProblem(StatusCodes.Status403Forbidden);

        // Signed-in users only, not the role policy: a creator demoted since the request must get the handler's 404, not a 403
        // that would confirm the job exists.
        api.MapGroup(ApiRoutes.Reports).WithTags(Tag).MapGet(ExportDownloadRoute, async (Guid id, DownloadExportHandler h, HttpContext http, CancellationToken ct) =>
            {
                var file = await h.HandleAsync(new DownloadExportQuery(id), ct);
                http.Response.Headers[NoSniffHeader] = NoSniff;
                return TypedResults.File(file.Content, file.ContentType, file.FileName);
            })
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status410Gone);

        return api;
    }
}
