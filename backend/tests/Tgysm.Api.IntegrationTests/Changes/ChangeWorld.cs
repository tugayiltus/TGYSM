using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.IntegrationTests.Changes;

/// <summary>Change test helpers on top of <see cref="TicketWorld"/>: Desk (Servis Masası) owns changes, Field (Saha Destek) works tasks.</summary>
public static class ChangeWorld
{
    public const string ChangesPath = "/api/v1/changes";
    public const string ImplementationPlan = "Sunucuyu güncelle";
    public const string BackoutPlan = "Eski imajı geri yükle";

    public static object DraftBody(string title = "Sunucu güncellemesi", string? implementationPlan = ImplementationPlan, string? backoutPlan = BackoutPlan) => new
    {
        title,
        justification = "Güvenlik yaması",
        affectedService = "Dosya sunucusu",
        risk = "medium",
        impact = "high",
        windowStart = "2026-12-05T20:00:00Z",
        windowEnd = "2026-12-05T22:00:00Z",
        implementationPlan,
        verificationPlan = "Paylaşımları dene",
        backoutPlan,
    };

    public static async Task<JsonElement> CreateAsync(ApiClient client, TicketWorld world, object? draft = null, IEnumerable<Guid>? assetIds = null)
    {
        using var response = await client.PostAsync(ChangesPath, new
        {
            draft = draft ?? DraftBody(),
            ownerTeamId = world.ServiceDesk.Id,
            ownerId = world.DeskUser.Id,
            assetIds = assetIds ?? [],
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    /// <summary>Seeds a task of the change directly; task creation fields for changes arrive with the work list.</summary>
    public static async Task AddTaskAsync(ApiFactory factory, JsonElement change, ChangePhase phase, Guid teamId, Guid? assigneeId = null)
    {
        var changeId = change.GetProperty("id").GetGuid();
        var now = DateTimeOffset.UtcNow;
        var number = TaskNumber.Format(now.Year, Random.Shared.NextInt64(1, 900_000));
        var task = WorkTask.Create(number, "Değişiklik görevi", null, teamId, assigneeId, Priority.P2, null, null, changeId, phase, change.GetProperty("createdById").GetGuid(), now);
        await TestData.SaveAsync(factory, db => db.WorkTasks.Add(task));
    }

    public static string PathOf(JsonElement change, string? action = null) =>
        action is null ? $"{ChangesPath}/{NumberOf(change)}" : $"{ChangesPath}/{NumberOf(change)}/{action}";

    public static string NumberOf(JsonElement change) => change.GetProperty("number").GetString()!;

    public static uint VersionOf(JsonElement change) => change.GetProperty("version").GetUInt32();
}
