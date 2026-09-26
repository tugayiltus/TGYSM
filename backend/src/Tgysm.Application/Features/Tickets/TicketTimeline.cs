using System.Text.Json;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tickets;

/// <summary>Adds timeline rows in the same unit of work as the change.</summary>
internal static class TicketTimeline
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static void Record(IAppDbContext db, Ticket ticket, TicketEventKind kind, Visibility visibility, Guid? actorUserId, DateTimeOffset now, object? data = null) =>
        db.TicketEvents.Add(TicketEvent.Record(ticket.Id, kind, visibility, actorUserId, JsonSerializer.Serialize(data ?? new { }, Json), now));
}
