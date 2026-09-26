namespace Tgysm.Application.Abstractions;

public interface IAuditLog
{
    /// <summary>
    /// Adds an audit row to the current unit of work; it is saved in the same transaction as the change.
    /// Never pass passwords, hashes or tokens in <paramref name="oldValues"/> / <paramref name="newValues"/>.
    /// </summary>
    void Record(
        string action,
        string entityType,
        Guid? entityId,
        object? oldValues = null,
        object? newValues = null,
        string? reason = null,
        Guid? actorUserId = null);
}
