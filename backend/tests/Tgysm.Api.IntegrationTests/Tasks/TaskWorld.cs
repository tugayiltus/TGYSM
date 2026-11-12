using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;

namespace Tgysm.Api.IntegrationTests.Tasks;

/// <summary>Task test helpers on top of <see cref="TicketWorld"/>: Desk works in Servis Masası, Field in Saha Destek.</summary>
public static class TaskWorld
{
    public const string TasksPath = "/api/v1/tasks";

    public static async Task<JsonElement> CreateAsync(ApiClient client, object body)
    {
        using var response = await client.PostAsync(TasksPath, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    public static Task<JsonElement> CreateForTeamAsync(ApiClient client, Guid teamId, Guid? assigneeId = null, string? ticketNumber = null, string? dueDate = null) =>
        CreateAsync(client, new { title = "Yedek al", description = "Gece yedeği", teamId, assigneeId, priority = "p2", dueDate, ticketNumber });

    public static string PathOf(JsonElement task, string? action = null) =>
        action is null ? $"{TasksPath}/{NumberOf(task)}" : $"{TasksPath}/{NumberOf(task)}/{action}";

    public static string NumberOf(JsonElement task) => task.GetProperty("number").GetString()!;

    public static uint VersionOf(JsonElement task) => task.GetProperty("version").GetUInt32();
}
