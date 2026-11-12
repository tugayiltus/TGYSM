using System.Text.Json;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Tasks;

namespace Tgysm.Application.Features.Tasks;

internal static class TaskHistory
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static void Record(IAppDbContext db, WorkTask task, TaskEventKind kind, Guid? actorUserId, DateTimeOffset now, object? data = null) =>
        db.TaskEvents.Add(TaskEvent.Record(task.Id, kind, actorUserId, JsonSerializer.Serialize(data ?? new { }, Json), now));
}
