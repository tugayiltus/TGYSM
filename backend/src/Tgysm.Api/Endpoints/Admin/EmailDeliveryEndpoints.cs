using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Http;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Notifications.Deliveries;
using Tgysm.Domain.Notifications;

namespace Tgysm.Api.Endpoints.Admin;

internal static class EmailDeliveryEndpoints
{
    private const string Tag = "Admin: Email deliveries";
    private const string StatusParameter = "status";

    public static RouteGroupBuilder MapEmailDeliveryEndpoints(this RouteGroupBuilder admin)
    {
        var deliveries = admin.MapGroup(ApiRoutes.EmailDeliveries).WithTags(Tag);
        deliveries.MapGet("", ListAsync);
        deliveries.MapPost("/{id:guid}/retry", async (Guid id, RetryEmailDeliveryHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new RetryEmailDeliveryCommand(id), ct)))
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return admin;
    }

    private static async Task<Ok<PagedResult<EmailDeliveryItem>>> ListAsync(
        string? status, string? eventType, string? recipient, DateTimeOffset? from, DateTimeOffset? to,
        int? page, int? pageSize, ListEmailDeliveriesHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ListEmailDeliveriesQuery(
                QueryParsing.ParseEnum<DeliveryStatus>(status, StatusParameter), eventType, recipient, from, to,
                page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize),
            cancellationToken));
}
