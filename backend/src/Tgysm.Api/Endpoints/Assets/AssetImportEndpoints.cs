using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Tgysm.Api.Http;
using Tgysm.Api.Security;
using Tgysm.Application.Features.Assets.Import;

namespace Tgysm.Api.Endpoints.Assets;

internal static class AssetImportEndpoints
{
    private const string Tag = "Asset import";
    private const string CsvContentType = "text/csv; charset=utf-8";

    /// <summary>Multipart overhead allowed above the file limit, so an oversized file reaches the handler's clear error.</summary>
    private const long MultipartOverheadBytes = 1024 * 1024;

    public static RouteGroupBuilder MapAssetImportEndpoints(this RouteGroupBuilder api)
    {
        var import = api.MapGroup(ApiRoutes.AssetImport).WithTags(Tag).RequireAuthorization(Policies.ItStaffOrAdmin);

        import.MapGet("/template", async (GetAssetImportTemplateHandler handler, CancellationToken ct) =>
        {
            var template = await handler.HandleAsync(ct);
            return TypedResults.File(template.Content, CsvContentType, template.FileName);
        });

        // Antiforgery is checked for every unsafe /api request by CsrfValidationMiddleware (header token), not by form binding.
        import.MapPost("/preview", PreviewAsync)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(PreviewAssetImportHandler.MaxFileBytes + MultipartOverheadBytes))
            .ProducesProblem(StatusCodes.Status400BadRequest);

        import.MapPost("/{jobId:guid}/commit", async (Guid jobId, CommitAssetImportHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new CommitAssetImportCommand(jobId), ct)))
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return import;
    }

    private static async Task<Ok<AssetImportJobItem>> PreviewAsync(IFormFile file, PreviewAssetImportHandler handler, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        return TypedResults.Ok(await handler.HandleAsync(new PreviewAssetImportCommand(file.FileName, file.Length, content), cancellationToken));
    }
}
