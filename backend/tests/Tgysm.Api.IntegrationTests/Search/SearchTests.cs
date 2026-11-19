using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Assets;
using Tgysm.Api.IntegrationTests.Changes;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Knowledge;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Application.Features.Search;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;

namespace Tgysm.Api.IntegrationTests.Search;

[Collection(ApiCollection.Name)]
public sealed class SearchTests(ApiFactory factory) : IAsyncLifetime
{
    private const string SearchPath = "/api/v1/search";
    private const string TicketGroup = "ticket";
    private const string ChangeGroup = "change";
    private const string TaskGroup = "task";
    private const string AssetGroup = "asset";
    private const string ArticleGroup = "article";
    private const string SerialNumber = "SN-ZX81-QQ";
    private const string MissText = "hicbirsey";
    private const int OverLimit = SearchLimits.PerType + 1;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<JsonElement> SearchAsync(ApiClient client, string q)
    {
        using var response = await client.GetAsync($"{SearchPath}?q={Uri.EscapeDataString(q)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private static JsonElement[] Groups(JsonElement result) => [.. result.GetProperty("groups").EnumerateArray()];

    private static string[] GroupTypes(JsonElement result) => [.. Groups(result).Select(g => g.GetProperty("type").GetString()!)];

    private static string[] Keys(JsonElement result, string type) =>
        [.. Groups(result).Where(g => g.GetProperty("type").GetString() == type).SelectMany(g => g.GetProperty("hits").EnumerateArray()).Select(h => h.GetProperty("key").GetString()!)];

    [Fact]
    public async Task Desk_finds_an_own_team_ticket_by_a_title_word_and_not_another_teams_ticket()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee, "Toplantı odası projektörü");

        var desk = await SearchAsync(world.Desk, "projektör");
        var field = await SearchAsync(world.Field, "projektör");

        Assert.Equal([TicketWorld.NumberOf(ticket)], Keys(desk, TicketGroup));
        Assert.Empty(Groups(field));
    }

    [Fact]
    public async Task Turkish_case_variants_give_the_same_hits()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee, "İnternet bağlantısı koptu");

        foreach (var q in new[] { "İNTERNET", "internet", "İnternet" })
        {
            Assert.Equal([TicketWorld.NumberOf(ticket)], Keys(await SearchAsync(world.Desk, q), TicketGroup));
        }
    }

    [Fact]
    public async Task Percent_and_underscore_match_literally()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var percent = await TicketWorld.CreateIncidentAsync(world.Employee, "Kota 100% doldu");
        await TicketWorld.CreateIncidentAsync(world.Employee, "Kota 1000 doldu");
        var underscore = await TicketWorld.CreateIncidentAsync(world.Employee, "Kod a_b hatası");
        await TicketWorld.CreateIncidentAsync(world.Employee, "Kod axb hatası");

        Assert.Equal([TicketWorld.NumberOf(percent)], Keys(await SearchAsync(world.Desk, "100%"), TicketGroup));
        Assert.Equal([TicketWorld.NumberOf(underscore)], Keys(await SearchAsync(world.Desk, "a_b"), TicketGroup));
    }

    [Fact]
    public async Task A_full_ticket_number_gives_an_exact_hit_only_when_the_viewer_may_see_it()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee, "Klavye bozuk");
        var number = TicketWorld.NumberOf(ticket);

        var seen = await SearchAsync(world.Desk, number);
        var hidden = await SearchAsync(world.Field, number);
        var miss = await SearchAsync(world.Field, MissText);

        var exact = seen.GetProperty("exact");
        Assert.Equal("ticket", exact.GetProperty("type").GetString());
        Assert.Equal(number, exact.GetProperty("key").GetString());
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("exact").ValueKind);
        Assert.Equal(miss.GetRawText(), hidden.GetRawText());
    }

    [Fact]
    public async Task Change_and_task_numbers_give_exact_hits()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var task = await ChangeWorld.CreateTaskAsync(world.Desk, change, "implementation", world.ServiceDesk.Id, world.DeskUser.Id);

        var byChange = await SearchAsync(world.Desk, ChangeWorld.NumberOf(change));
        var byTask = await SearchAsync(world.Desk, task.GetProperty("number").GetString()!);

        Assert.Equal("change", byChange.GetProperty("exact").GetProperty("type").GetString());
        Assert.Equal(ChangeWorld.NumberOf(change), byChange.GetProperty("exact").GetProperty("key").GetString());
        Assert.Equal("task", byTask.GetProperty("exact").GetProperty("type").GetString());
        Assert.Equal(task.GetProperty("number").GetString(), byTask.GetProperty("exact").GetProperty("key").GetString());
    }

    [Fact]
    public async Task Staff_find_an_asset_by_a_tag_fragment_and_by_serial()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var store = await TestData.LocationAsync(factory, "Depo");
        var asset = await AssetCatalogueTests.CreateAsync(
            world.Desk, new { modelId = await TestData.ModelAsync(factory), locationId = store.Id, serialNumber = SerialNumber });
        var tag = asset.GetProperty("assetTag").GetString()!;

        var byTag = await SearchAsync(world.Desk, tag[^6..]);
        var bySerial = await SearchAsync(world.Desk, "zx81");
        var byFullTag = await SearchAsync(world.Desk, tag);

        Assert.Equal([tag], Keys(byTag, AssetGroup));
        Assert.Equal([tag], Keys(bySerial, AssetGroup));
        Assert.Equal(tag, byFullTag.GetProperty("exact").GetProperty("key").GetString());
    }

    [Fact]
    public async Task Employee_gets_only_ticket_and_article_groups_for_readable_records()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var store = await TestData.LocationAsync(factory, "Depo");
        await AssetCatalogueTests.CreateAsync(world.Desk, new { modelId = await TestData.ModelAsync(factory), locationId = store.Id });
        await TicketWorld.CreateIncidentAsync(world.Employee, "Yazıcı sorunu");
        await TicketWorld.CreateIncidentAsync(world.OtherEmployee, "Yazıcı arızası");
        await ChangeWorld.CreateAsync(world.Desk, world, ChangeWorld.DraftBody("Yazıcı sunucusu"));
        var open = await ArticleApiTests.PublishedAsync(world.Desk, "Yazıcı ekleme", visibility: "employees");
        await ArticleApiTests.PublishedAsync(world.Desk, "Yazıcı iç notu", visibility: "internal");

        var result = await SearchAsync(world.Employee, "yazıcı");

        Assert.Equal([TicketGroup, ArticleGroup], GroupTypes(result));
        Assert.Single(Keys(result, TicketGroup));
        Assert.Equal([open.GetProperty("id").GetGuid().ToString()], Keys(result, ArticleGroup));
    }

    [Fact]
    public async Task Summary_access_change_is_found_by_title()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world, ChangeWorld.DraftBody("Yedekleme altyapısı"));
        await ChangeWorld.AddTaskAsync(factory, change, ChangePhase.Backout, world.FieldTeam.Id);

        var result = await SearchAsync(world.Field, "altyapı");

        Assert.Equal([ChangeWorld.NumberOf(change)], Keys(result, ChangeGroup));
    }

    [Fact]
    public async Task More_than_the_per_type_limit_returns_the_limit_and_has_more()
    {
        var world = await TicketWorld.CreateAsync(factory);
        for (var i = 0; i < OverLimit; i++)
        {
            await TicketWorld.CreateIncidentAsync(world.Employee, $"Fare bozuk {i}");
        }

        var group = Groups(await SearchAsync(world.Desk, "fare")).Single();

        Assert.Equal(TicketGroup, group.GetProperty("type").GetString());
        Assert.Equal(SearchLimits.PerType, group.GetProperty("hits").GetArrayLength());
        Assert.True(group.GetProperty("hasMore").GetBoolean());
    }

    [Theory]
    [InlineData("a")]
    [InlineData("   ")]
    [InlineData(" a ")]
    public async Task Too_short_or_blank_query_is_rejected(string q)
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var response = await world.Desk.GetAsync($"{SearchPath}?q={Uri.EscapeDataString(q)}");

        await AssertInvalidAsync(response);
    }

    [Fact]
    public async Task Too_long_or_missing_query_is_rejected()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var tooLong = await world.Desk.GetAsync($"{SearchPath}?q={new string('a', SearchLimits.MaxLength + 1)}");
        using var missing = await world.Desk.GetAsync(SearchPath);

        await AssertInvalidAsync(tooLong);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    private static async Task AssertInvalidAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.SearchQueryInvalid, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
