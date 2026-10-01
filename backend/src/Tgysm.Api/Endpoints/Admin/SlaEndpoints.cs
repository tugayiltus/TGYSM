using Tgysm.Api.Http;
using Tgysm.Application.Features.Sla.Admin;

namespace Tgysm.Api.Endpoints.Admin;

public sealed record UpdateCalendarRequest(uint Version, string TimeZone, IReadOnlyList<WorkingHoursItem> Hours);

internal static class SlaEndpoints
{
    private const string Tag = "Admin: SLA";
    private const string CalendarRoute = "/calendar";
    private const string HolidaysRoute = "/holidays";
    private const string PolicyRoute = "/policy";

    public static RouteGroupBuilder MapSlaEndpoints(this RouteGroupBuilder admin)
    {
        var sla = admin.MapGroup(ApiRoutes.Sla).WithTags(Tag);

        sla.MapGet(CalendarRoute, async (GetCalendarHandler h, CancellationToken ct) => TypedResults.Ok(await h.HandleAsync(ct)))
            .ProducesProblem(StatusCodes.Status404NotFound);
        sla.MapPut(CalendarRoute, async (UpdateCalendarRequest r, UpdateCalendarHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new UpdateCalendarCommand(r.Version, r.TimeZone, r.Hours), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sla.MapGet(HolidaysRoute, async (ListHolidaysHandler h, CancellationToken ct) => TypedResults.Ok(await h.HandleAsync(ct)));
        sla.MapPost(HolidaysRoute, async (AddHolidayCommand command, AddHolidayHandler h, CancellationToken ct) =>
            {
                var item = await h.HandleAsync(command, ct);
                return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.Sla}{HolidaysRoute}/{item.Id}", item);
            })
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
        sla.MapDelete($"{HolidaysRoute}/{{id:guid}}", async (Guid id, RemoveHolidayHandler h, CancellationToken ct) =>
            {
                await h.HandleAsync(new RemoveHolidayCommand(id), ct);
                return TypedResults.NoContent();
            })
            .ProducesProblem(StatusCodes.Status404NotFound);

        sla.MapGet(PolicyRoute, async (GetPolicyHandler h, CancellationToken ct) => TypedResults.Ok(await h.HandleAsync(ct)))
            .ProducesProblem(StatusCodes.Status404NotFound);
        sla.MapPost(PolicyRoute, async (CreatePolicyVersionCommand command, CreatePolicyVersionHandler h, CancellationToken ct) =>
            {
                var item = await h.HandleAsync(command, ct);
                return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.Sla}{PolicyRoute}", item);
            })
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return admin;
    }
}
