namespace Tgysm.Domain.Tickets;

/// <summary>Incident → related request (spec §3.8).</summary>
public sealed class TicketRelation
{
    private TicketRelation()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid IncidentId { get; private init; }
    public Guid RequestId { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    public static TicketRelation Create(Guid incidentId, Guid requestId, DateTimeOffset now) =>
        new() { IncidentId = incidentId, RequestId = requestId, CreatedAt = now };
}

/// <summary>Ticket ↔ asset link, visible from both sides within access scope.</summary>
public sealed class TicketAsset
{
    private TicketAsset()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid TicketId { get; private init; }
    public Guid AssetId { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    public static TicketAsset Create(Guid ticketId, Guid assetId, DateTimeOffset now) =>
        new() { TicketId = ticketId, AssetId = assetId, CreatedAt = now };
}
