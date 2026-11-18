using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Changes;

namespace Tgysm.Api.IntegrationTests.Changes;

[Collection(ApiCollection.Name)]
public sealed class ChangeConflictTests(ApiFactory factory) : IAsyncLifetime
{
    private const string MailService = "Mail sunucusu";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static object Draft(string title, string? service, string start, string end) => new
    {
        title,
        justification = "Bakım",
        affectedService = service,
        risk = "medium",
        impact = "high",
        windowStart = start,
        windowEnd = end,
        implementationPlan = ChangeWorld.ImplementationPlan,
        verificationPlan = "Dene",
        backoutPlan = ChangeWorld.BackoutPlan,
    };

    private static string Hour(int hour) => $"2026-12-05T{hour:00}:00:00Z";

    private async Task<Guid> AssetAsync(TicketWorld world, string tag) => await TestData.LegacyAssetAsync(factory, tag, world.Office.Id);

    private static async Task<JsonElement> CreateAsync(
        ApiClient creator, Guid teamId, Guid ownerId, object draft, params Guid[] assets)
    {
        using var response = await creator.PostAsync(ChangeWorld.ChangesPath, new { draft, ownerTeamId = teamId, ownerId, assetIds = assets });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await creator.ReadJsonAsync<JsonElement>(response);
    }

    /// <summary>Creates a change and submits it; it waits for approval.</summary>
    private static async Task<JsonElement> PendingAsync(TicketWorld world, object draft, params Guid[] assets)
    {
        var change = await CreateAsync(world.Desk, world.ServiceDesk.Id, world.DeskUser.Id, draft, assets);
        await ChangeWorld.CreateTaskAsync(world.Desk, change, "implementation", world.ServiceDesk.Id, world.DeskUser.Id);
        return await ChangeWorld.ActAsync(world.Desk, await ChangeWorld.DetailAsync(world.Desk, change), "submit");
    }

    private static async Task<JsonElement> ScheduledAsync(TicketWorld world, object draft, params Guid[] assets)
    {
        var submitted = await PendingAsync(world, draft, assets);
        return await ChangeWorld.ActAsync(world.Admin, submitted, "approval/approve",
            new { approvalVersion = submitted.GetProperty("approval").GetProperty("version").GetUInt32() });
    }

    private static async Task<JsonElement> ConflictsOfAsync(ApiClient client, JsonElement change) =>
        (await ChangeWorld.DetailAsync(client, change)).GetProperty("conflicts");

    [Fact]
    public async Task Shared_asset_and_overlapping_windows_conflict_both_ways_with_reason_asset()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var asset = await AssetAsync(world, "SRV-1");
        var first = await ScheduledAsync(world, Draft("Birinci", null, Hour(8), Hour(10)), asset);
        var second = await ScheduledAsync(world, Draft("İkinci", null, Hour(9), Hour(11)), asset);

        var onFirst = await ConflictsOfAsync(world.Desk, first);
        var onSecond = await ConflictsOfAsync(world.Desk, second);

        var conflict = Assert.Single(onFirst.EnumerateArray());
        Assert.Equal(ChangeWorld.NumberOf(second), conflict.GetProperty("number").GetString());
        Assert.Equal("İkinci", conflict.GetProperty("title").GetString());
        Assert.Equal("asset", conflict.GetProperty("reason").GetString());
        Assert.Equal(DateTimeOffset.Parse(Hour(9)), conflict.GetProperty("windowStart").GetDateTimeOffset());
        Assert.Equal(DateTimeOffset.Parse(Hour(11)), conflict.GetProperty("windowEnd").GetDateTimeOffset());
        Assert.Equal(ChangeWorld.NumberOf(first), Assert.Single(onSecond.EnumerateArray()).GetProperty("number").GetString());
    }

    [Fact]
    public async Task Same_service_text_without_a_shared_asset_conflicts_with_reason_service()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var first = await ScheduledAsync(world, Draft("Birinci", MailService, Hour(8), Hour(10)), await AssetAsync(world, "SRV-1"));
        await ScheduledAsync(world, Draft("İkinci", " MAIL SUNUCUSU ", Hour(9), Hour(11)), await AssetAsync(world, "SRV-2"));

        var conflict = Assert.Single((await ConflictsOfAsync(world.Desk, first)).EnumerateArray());

        Assert.Equal("service", conflict.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Overlapping_windows_without_a_shared_asset_or_service_do_not_conflict()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var first = await ScheduledAsync(world, Draft("Birinci", MailService, Hour(8), Hour(10)), await AssetAsync(world, "SRV-1"));
        await ScheduledAsync(world, Draft("İkinci", "Dosya sunucusu", Hour(9), Hour(11)), await AssetAsync(world, "SRV-2"));

        Assert.Empty((await ConflictsOfAsync(world.Desk, first)).EnumerateArray());
    }

    [Fact]
    public async Task A_draft_never_conflicts_with_a_scheduled_change()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var asset = await AssetAsync(world, "SRV-1");
        var scheduled = await ScheduledAsync(world, Draft("Planlı", MailService, Hour(8), Hour(10)), asset);
        var draft = await CreateAsync(world.Desk, world.ServiceDesk.Id, world.DeskUser.Id, Draft("Taslak", MailService, Hour(9), Hour(11)), asset);

        Assert.Empty((await ConflictsOfAsync(world.Desk, scheduled)).EnumerateArray());
        Assert.Empty((await ConflictsOfAsync(world.Desk, draft)).EnumerateArray());
    }

    [Fact]
    public async Task Touching_windows_do_not_conflict()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var asset = await AssetAsync(world, "SRV-1");
        var first = await ScheduledAsync(world, Draft("Birinci", null, Hour(8), Hour(10)), asset);
        await ScheduledAsync(world, Draft("İkinci", null, Hour(10), Hour(12)), asset);

        Assert.Empty((await ConflictsOfAsync(world.Desk, first)).EnumerateArray());
    }

    [Fact]
    public async Task A_change_the_viewer_cannot_see_is_reported_without_number_and_title()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var asset = await AssetAsync(world, "SRV-1");
        var mine = await ScheduledAsync(world, Draft("Benim", null, Hour(8), Hour(10)), asset);
        var hidden = await CreateAsync(world.Field, world.FieldTeam.Id, world.FieldUser.Id, Draft("Gizli", null, Hour(9), Hour(11)), asset);
        await ChangeWorld.CreateTaskAsync(world.Field, hidden, "implementation", world.FieldTeam.Id, world.FieldUser.Id);
        var submitted = await ChangeWorld.ActAsync(world.Field, await ChangeWorld.DetailAsync(world.Field, hidden), "submit");
        await ChangeWorld.ActAsync(world.Admin, submitted, "approval/approve",
            new { approvalVersion = submitted.GetProperty("approval").GetProperty("version").GetUInt32() });

        var asDesk = Assert.Single((await ConflictsOfAsync(world.Desk, mine)).EnumerateArray());
        var asAdmin = Assert.Single((await ConflictsOfAsync(world.Admin, mine)).EnumerateArray());

        Assert.Equal(JsonValueKind.Null, asDesk.GetProperty("number").ValueKind);
        Assert.Equal(JsonValueKind.Null, asDesk.GetProperty("title").ValueKind);
        Assert.Equal("asset", asDesk.GetProperty("reason").GetString());
        Assert.Equal(DateTimeOffset.Parse(Hour(9)), asDesk.GetProperty("windowStart").GetDateTimeOffset());
        Assert.Equal("Gizli", asAdmin.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Change_approvals_count_the_conflicts_of_each_pending_change()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var asset = await AssetAsync(world, "SRV-1");
        await ScheduledAsync(world, Draft("Planlı", null, Hour(8), Hour(10)), asset);
        await PendingAsync(world, Draft("Bekleyen", null, Hour(9), Hour(11)), asset);

        var items = (await TicketWorld.ReadAsync(world.Admin, "/api/v1/change-approvals")).EnumerateArray().ToList();

        Assert.Equal(1, Assert.Single(items).GetProperty("conflictCount").GetInt32());
    }

    [Fact]
    public async Task A_summary_viewer_gets_no_detail_and_so_no_conflicts()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var asset = await AssetAsync(world, "SRV-1");
        await ScheduledAsync(world, Draft("Birinci", null, Hour(8), Hour(10)), asset);
        var second = await ScheduledAsync(world, Draft("İkinci", null, Hour(9), Hour(11)), asset);
        await ChangeWorld.AddTaskAsync(factory, second, ChangePhase.Backout, world.FieldTeam.Id);

        var seen = await TicketWorld.ReadAsync(world.Field, ChangeWorld.PathOf(second));

        Assert.Equal("summary", seen.GetProperty("access").GetString());
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("detail").ValueKind);
        Assert.False(seen.GetProperty("summary").TryGetProperty("conflicts", out _));
    }
}
