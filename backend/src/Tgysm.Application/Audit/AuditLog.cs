using System.Text.Json;
using System.Text.Json.Serialization;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Audit;

internal sealed class AuditLog(IAppDbContext db, ICurrentActor actor, TimeProvider clock) : IAuditLog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public void Record(
        string action,
        string entityType,
        Guid? entityId,
        object? oldValues = null,
        object? newValues = null,
        string? reason = null,
        Guid? actorUserId = null)
    {
        db.AuditEvents.Add(AuditEvent.Create(
            clock.GetUtcNow(),
            actorUserId ?? actor.UserId,
            action,
            entityType,
            entityId,
            reason,
            Serialize(oldValues),
            Serialize(newValues),
            actor.CorrelationId));
    }

    private static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, value.GetType(), JsonOptions);
}
