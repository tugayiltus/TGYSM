using Tgysm.Api.Http;
using Tgysm.Application.Features.Notifications.Templates;

namespace Tgysm.Api.Endpoints.Admin;

public sealed record UpdateEmailTemplateRequest(uint Version, string Subject, string Body);

internal static class EmailTemplateEndpoints
{
    private const string Tag = "Admin: Email templates";

    public static RouteGroupBuilder MapEmailTemplateEndpoints(this RouteGroupBuilder admin)
    {
        var templates = admin.MapGroup(ApiRoutes.EmailTemplates).WithTags(Tag);
        templates.MapGet("", async (ListEmailTemplatesHandler h, CancellationToken ct) => TypedResults.Ok(await h.HandleAsync(ct)));
        templates.MapPut("/{eventType}", async (string eventType, UpdateEmailTemplateRequest r, UpdateEmailTemplateHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new UpdateEmailTemplateCommand(eventType, r.Version, r.Subject, r.Body), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return admin;
    }
}
