using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Changes;

namespace Tgysm.Application.Features.Changes;

internal static class ChangeHistory
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static void Record(IAppDbContext db, ChangeRequest change, ChangeEventKind kind, Guid? actorUserId, DateTimeOffset now, object? data = null) =>
        db.ChangeEvents.Add(ChangeEvent.Record(change.Id, kind, actorUserId, JsonSerializer.Serialize(data ?? new { }, Json), now));
}

public sealed record GetChangeTimelineQuery(string Number);

public sealed record ChangeTimelineItem(Guid Id, ChangeEventKind Kind, string? ActorName, JsonElement Data, DateTimeOffset OccurredAt);

/// <summary>Change history in order. Full access only: a working summary gets "not found" (ADR-0031).</summary>
public sealed class GetChangeTimelineHandler(IAppDbContext db, ChangeScope scope)
{
    private const string NameSeparator = " ";

    public async Task<IReadOnlyList<ChangeTimelineItem>> HandleAsync(GetChangeTimelineQuery query, CancellationToken cancellationToken)
    {
        var (change, _, access) = await scope.LoadAsync(query.Number, cancellationToken);
        if (access != ChangeAccess.Full)
        {
            throw new NotFoundException(Domain.Audit.AuditEntityTypes.Change);
        }

        var rows = await db.ChangeEvents.AsNoTracking().Where(e => e.ChangeId == change.Id)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                e.Kind,
                e.DataJson,
                e.OccurredAt,
                Actor = db.Users.Where(u => u.Id == e.ActorUserId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => new ChangeTimelineItem(r.Id, r.Kind, r.Actor, JsonSerializer.Deserialize<JsonElement>(r.DataJson), r.OccurredAt))];
    }
}
