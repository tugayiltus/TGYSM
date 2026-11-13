using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tasks;
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

    /// <summary>Adds a task of the change through <c>POST /api/v1/tasks</c> (owner team member or admin).</summary>
    public static Task<JsonElement> CreateTaskAsync(ApiClient client, JsonElement change, string phase, Guid teamId, Guid? assigneeId = null) =>
        TaskWorld.CreateAsync(client, new { title = "Değişiklik görevi", teamId, assigneeId, priority = "p2", changeNumber = NumberOf(change), phase });

    /// <summary>Posts a change action that must succeed and returns the fresh change detail.</summary>
    public static async Task<JsonElement> ActAsync(ApiClient client, JsonElement change, string action, object? body = null)
    {
        using var response = await client.PostAsync(PathOf(change, action), body ?? new { version = VersionOf(change) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    /// <summary>Posts a task action that must succeed and returns the fresh task detail.</summary>
    public static async Task<JsonElement> TaskActAsync(ApiClient client, JsonElement task, string action, object? body = null)
    {
        using var response = await client.PostAsync(TaskWorld.PathOf(task, action), body ?? new { version = TaskWorld.VersionOf(task) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    /// <summary>Reads the change detail as the given (full access) viewer.</summary>
    public static async Task<JsonElement> DetailAsync(ApiClient client, JsonElement change) =>
        (await TicketWorld.ReadAsync(client, PathOf(change))).GetProperty("detail");

    /// <summary>
    /// An approved change owned by Desk, with one Uygulama task of Servis Masası (returned as <c>Task</c>) so Desk can
    /// work both the change and the task.
    /// </summary>
    public static async Task<(JsonElement Change, JsonElement Task)> ScheduledAsync(TicketWorld world, Func<JsonElement, Task>? beforeSubmit = null)
    {
        var change = await CreateAsync(world.Desk, world);
        var task = await CreateTaskAsync(world.Desk, change, "implementation", world.ServiceDesk.Id, world.DeskUser.Id);
        if (beforeSubmit is not null)
        {
            await beforeSubmit(change);
        }

        change = await DetailAsync(world.Desk, change);
        var submitted = await ActAsync(world.Desk, change, "submit");
        var approved = await ActAsync(world.Admin, submitted, "approval/approve",
            new { approvalVersion = submitted.GetProperty("approval").GetProperty("version").GetUInt32() });
        return (approved, task);
    }

    public static string PathOf(JsonElement change, string? action = null) =>
        action is null ? $"{ChangesPath}/{NumberOf(change)}" : $"{ChangesPath}/{NumberOf(change)}/{action}";

    public static string NumberOf(JsonElement change) => change.GetProperty("number").GetString()!;

    public static uint VersionOf(JsonElement change) => change.GetProperty("version").GetUInt32();
}
