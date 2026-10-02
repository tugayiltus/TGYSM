namespace Tgysm.Domain.Tickets;

/// <summary>Ticket ↔ knowledge article link (spec §6.1); one link per pair.</summary>
public sealed class TicketArticle
{
    private TicketArticle()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid TicketId { get; private init; }
    public Guid ArticleId { get; private init; }
    public Guid LinkedBy { get; private init; }
    public DateTimeOffset LinkedAt { get; private init; }

    public static TicketArticle Create(Guid ticketId, Guid articleId, Guid linkedBy, DateTimeOffset now) =>
        new() { TicketId = ticketId, ArticleId = articleId, LinkedBy = linkedBy, LinkedAt = now };
}
