using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;

namespace Tgysm.Api.IntegrationTests.Tickets;

[Collection(ApiCollection.Name)]
public sealed class TicketScopeTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<List<string>> NumbersAsync(ApiClient client, string query = "")
    {
        var page = await TicketWorld.ReadAsync(client, $"{TicketWorld.TicketsPath}{query}");
        return [.. page.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("number").GetString()!)];
    }

    [Fact]
    public async Task Employee_sees_only_own_tickets_and_others_get_404()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var mine = await TicketWorld.CreateIncidentAsync(world.Employee);
        var theirs = await TicketWorld.CreateIncidentAsync(world.OtherEmployee, "Başkasının sorunu");

        var list = await NumbersAsync(world.Employee);
        using var other = await world.Employee.GetAsync(TicketWorld.PathOf(theirs));
        using var missing = await world.Employee.GetAsync($"{TicketWorld.TicketsPath}/INC-2026-999999");
        using var searchOther = await world.Employee.GetAsync($"{TicketWorld.TicketsPath}?search={TicketWorld.NumberOf(theirs)}");

        Assert.Equal([TicketWorld.NumberOf(mine)], list);
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(0, (await world.Employee.ReadJsonAsync<JsonElement>(searchOther)).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task It_staff_sees_team_tickets_but_not_other_teams()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        var desk = await NumbersAsync(world.Desk, "?queue=team");
        var field = await NumbersAsync(world.Field);
        using var fieldDetail = await world.Field.GetAsync(TicketWorld.PathOf(ticket));
        var admin = await NumbersAsync(world.Admin);

        Assert.Equal([TicketWorld.NumberOf(ticket)], desk);
        Assert.Empty(field);
        Assert.Equal(HttpStatusCode.NotFound, fieldDetail.StatusCode);
        Assert.Equal([TicketWorld.NumberOf(ticket)], admin);
    }

    [Fact]
    public async Task It_staff_sees_own_requests_outside_their_teams()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var own = await TicketWorld.CreateIncidentAsync(world.Field, "Ekranım bozuk");

        Assert.Equal([TicketWorld.NumberOf(own)], await NumbersAsync(world.Field, "?queue=mine"));
        Assert.Equal(HttpStatusCode.OK, (await world.Field.GetAsync(TicketWorld.PathOf(own))).StatusCode);
    }

    [Fact]
    public async Task List_filters_and_searches_within_scope()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var printer = await TicketWorld.CreateIncidentAsync(world.Employee, "Yazıcı çalışmıyor");
        await TicketWorld.CreateIncidentAsync(world.Employee, "Ağ bağlantısı yok");
        var request = await world.CreateRequestAsync(world.Employee, approval: true);

        Assert.Equal([TicketWorld.NumberOf(printer)], await NumbersAsync(world.Desk, "?search=yazici"));
        Assert.Equal([TicketWorld.NumberOf(printer)], await NumbersAsync(world.Desk, $"?search={TicketWorld.NumberOf(printer).ToLowerInvariant()}"));
        Assert.Equal([TicketWorld.NumberOf(request)], await NumbersAsync(world.Desk, "?kind=request"));
        Assert.Equal([TicketWorld.NumberOf(request)], await NumbersAsync(world.Desk, "?queue=on_hold"));
        Assert.Equal(3, (await NumbersAsync(world.Desk, "?queue=unassigned")).Count);
        Assert.Empty(await NumbersAsync(world.Desk, "?queue=assigned_to_me"));
        Assert.Equal(HttpStatusCode.BadRequest, (await world.Desk.GetAsync($"{TicketWorld.TicketsPath}?queue=everything")).StatusCode);
    }

    [Fact]
    public async Task Search_treats_like_wildcards_literally()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var percent = await TicketWorld.CreateIncidentAsync(world.Employee, "Disk 100% dolu");
        await TicketWorld.CreateIncidentAsync(world.Employee, "Disk 1000 dolu");
        var underscore = await TicketWorld.CreateIncidentAsync(world.Employee, "Dosya_adı hatalı");
        await TicketWorld.CreateIncidentAsync(world.Employee, "Dosyaxadı hatalı");

        Assert.Equal([TicketWorld.NumberOf(percent)], await NumbersAsync(world.Desk, $"?search={Uri.EscapeDataString("0%")}"));
        Assert.Equal([TicketWorld.NumberOf(underscore)], await NumbersAsync(world.Desk, $"?search={Uri.EscapeDataString("dosya_")}"));
    }

    [Fact]
    public async Task Employee_detail_has_no_internal_fields()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);

        var employeeView = await TicketWorld.ReadAsync(world.Employee, TicketWorld.PathOf(ticket));
        var deskView = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(ticket));

        Assert.False(employeeView.GetProperty("canWork").GetBoolean());
        Assert.True(deskView.GetProperty("canWork").GetBoolean());
        Assert.Equal(JsonValueKind.Null, employeeView.GetProperty("rootCause").ValueKind);
    }
}
