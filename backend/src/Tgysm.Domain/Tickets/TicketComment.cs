using Tgysm.Domain.Common;

namespace Tgysm.Domain.Tickets;

/// <summary>Public reply or internal note; plain text, never rendered as HTML.</summary>
public sealed class TicketComment : Entity
{
    public const int BodyMaxLength = 4000;

    private TicketComment()
    {
    }

    public Guid TicketId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public Visibility Visibility { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static TicketComment Create(Guid ticketId, Guid authorId, string body, Visibility visibility, DateTimeOffset now) =>
        new() { TicketId = ticketId, AuthorId = authorId, Body = body.Trim(), Visibility = visibility, CreatedAt = now };
}
