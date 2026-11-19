using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Tests.Changes;

public sealed class ChangeTicketLinkTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(TicketKind.Incident)]
    [InlineData(TicketKind.Request)]
    public void Prompted_is_allowed_for_every_ticket_kind(TicketKind ticketKind)
    {
        var changeId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var link = ChangeTicketLink.Create(changeId, ticketId, ticketKind, ChangeTicketLinkKind.Prompted, userId, Now);

        Assert.Equal(changeId, link.ChangeId);
        Assert.Equal(ticketId, link.TicketId);
        Assert.Equal(ChangeTicketLinkKind.Prompted, link.Kind);
        Assert.Equal(userId, link.CreatedById);
        Assert.Equal(Now, link.CreatedAt);
    }

    [Fact]
    public void Caused_is_allowed_for_an_incident()
    {
        var link = ChangeTicketLink.Create(Guid.NewGuid(), Guid.NewGuid(), TicketKind.Incident, ChangeTicketLinkKind.Caused, Guid.NewGuid(), Now);

        Assert.Equal(ChangeTicketLinkKind.Caused, link.Kind);
    }

    [Fact]
    public void Caused_is_rejected_for_a_request()
    {
        var ex = Assert.Throws<DomainException>(() =>
            ChangeTicketLink.Create(Guid.NewGuid(), Guid.NewGuid(), TicketKind.Request, ChangeTicketLinkKind.Caused, Guid.NewGuid(), Now));

        Assert.Equal(ErrorCodes.ChangeTicketLinkKindInvalid, ex.Code);
    }
}
