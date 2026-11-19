using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Changes;

/// <summary>Change ↔ ticket link. A request cannot be caused by a change; only an incident can.</summary>
public sealed class ChangeTicketLink
{
    private ChangeTicketLink()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid ChangeId { get; private init; }
    public Guid TicketId { get; private init; }
    public ChangeTicketLinkKind Kind { get; private init; }
    public Guid CreatedById { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    public static ChangeTicketLink Create(Guid changeId, Guid ticketId, TicketKind ticketKind, ChangeTicketLinkKind kind, Guid createdById, DateTimeOffset now)
    {
        if (kind == ChangeTicketLinkKind.Caused && ticketKind == TicketKind.Request)
        {
            throw new DomainException(ErrorCodes.ChangeTicketLinkKindInvalid, "A request cannot be caused by a change.");
        }

        return new ChangeTicketLink { ChangeId = changeId, TicketId = ticketId, Kind = kind, CreatedById = createdById, CreatedAt = now };
    }
}
