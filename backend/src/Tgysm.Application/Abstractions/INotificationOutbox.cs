namespace Tgysm.Application.Abstractions;

public interface INotificationOutbox
{
    /// <summary>
    /// Adds a notification event to the current unit of work; it is saved in the same transaction as the change
    /// that triggers it (ADR-0015). <paramref name="data"/> holds ids and reason codes only — never subject or
    /// resolution text — and <paramref name="protectedSecret"/> is for account-link events only and is never logged.
    /// </summary>
    void Add(
        string eventType,
        Guid? ticketId,
        Guid? userId,
        IReadOnlyDictionary<string, string>? data = null,
        string? protectedSecret = null);
}
