using Tgysm.Domain.Notifications;

namespace Tgysm.Domain.Tests.Notifications;

public sealed class OutboxSubjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ticket_event_keeps_ticket_id_and_names_the_ticket_as_subject()
    {
        var ticketId = Guid.NewGuid();

        var outboxEvent = OutboxEvent.Create(NotificationEventTypes.TicketCreated, ticketId, null, "{}", null, Now);

        Assert.Equal((ticketId, OutboxSubjectTypes.Ticket, (Guid?)ticketId), (outboxEvent.TicketId, outboxEvent.SubjectType, outboxEvent.SubjectId));
    }

    [Fact]
    public void Account_event_has_no_subject()
    {
        var outboxEvent = OutboxEvent.Create(NotificationEventTypes.AccountActivation, null, Guid.NewGuid(), "{}", null, Now);

        Assert.Null(outboxEvent.SubjectType);
        Assert.Null(outboxEvent.SubjectId);
    }

    [Fact]
    public void Task_event_names_the_task_and_no_ticket()
    {
        var taskId = Guid.NewGuid();
        var assignee = Guid.NewGuid();

        var outboxEvent = OutboxEvent.CreateForSubject(NotificationEventTypes.TaskAssigned, OutboxSubjectTypes.Task, taskId, assignee, "{}", Now);

        Assert.Null(outboxEvent.TicketId);
        Assert.Equal((OutboxSubjectTypes.Task, (Guid?)taskId, (Guid?)assignee), (outboxEvent.SubjectType, outboxEvent.SubjectId, outboxEvent.UserId));
    }
}
