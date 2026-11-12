namespace Tgysm.Domain.Notifications;

/// <summary>What an outbox event is about when it names a record (ADR-0031). Account events have no subject.</summary>
public static class OutboxSubjectTypes
{
    public const int MaxLength = 20;
    public const string Ticket = "ticket";
    public const string Task = "task";
}
