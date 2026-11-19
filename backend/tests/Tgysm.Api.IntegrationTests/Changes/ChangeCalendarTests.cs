using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Application.Features.Changes;

namespace Tgysm.Api.IntegrationTests.Changes;

/// <summary>The maintenance calendar and the "this week" queue (Phase 9). Company time is Europe/Istanbul (UTC+3 in winter).</summary>
public sealed class ChangeCalendarTests(FakeClockApiFactory factory) : IClassFixture<FakeClockApiFactory>, IAsyncLifetime
{
    private const string CalendarPath = "/api/v1/changes/calendar";
    private const string RangeCode = "changes.calendar_range";
    private const string RangeFrom = "2026-12-01";
    private const string RangeTo = "2026-12-31";

    // A Wednesday. A FakeTimeProvider only moves forward, so only one test sets it, before it logs anyone in.
    private static readonly DateTimeOffset Wednesday = new(2026, 12, 2, 9, 0, 0, TimeSpan.Zero);

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static object Draft(string title, string start, string end) => new
    {
        title,
        justification = "Bakım",
        risk = "medium",
        impact = "high",
        windowStart = start,
        windowEnd = end,
        implementationPlan = ChangeWorld.ImplementationPlan,
        verificationPlan = "Dene",
        backoutPlan = ChangeWorld.BackoutPlan,
    };

    private static string Day(int day, int hour) => $"2026-12-{day:00}T{hour:00}:00:00Z";

    private static async Task<JsonElement> CreateAsync(ApiClient client, Guid teamId, Guid ownerId, object draft, params Guid[] assets)
    {
        using var response = await client.PostAsync(ChangeWorld.ChangesPath, new { draft, ownerTeamId = teamId, ownerId, assetIds = assets });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private static async Task<JsonElement> PendingAsync(ApiClient client, Guid teamId, Guid ownerId, object draft, params Guid[] assets)
    {
        var change = await CreateAsync(client, teamId, ownerId, draft, assets);
        await ChangeWorld.CreateTaskAsync(client, change, "implementation", teamId, ownerId);
        return await ChangeWorld.ActAsync(client, await ChangeWorld.DetailAsync(client, change), "submit");
    }

    private static async Task<JsonElement> ScheduledAsync(TicketWorld world, ApiClient client, Guid teamId, Guid ownerId, object draft, params Guid[] assets)
    {
        var submitted = await PendingAsync(client, teamId, ownerId, draft, assets);
        return await ChangeWorld.ActAsync(world.Admin, submitted, "approval/approve",
            new { approvalVersion = submitted.GetProperty("approval").GetProperty("version").GetUInt32() });
    }

    private static Task<JsonElement> DeskScheduledAsync(TicketWorld world, object draft, params Guid[] assets) =>
        ScheduledAsync(world, world.Desk, world.ServiceDesk.Id, world.DeskUser.Id, draft, assets);

    private static async Task<List<JsonElement>> CalendarAsync(ApiClient client, string from = RangeFrom, string to = RangeTo)
    {
        using var response = await client.GetAsync($"{CalendarPath}?from={from}&to={to}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await client.ReadJsonAsync<JsonElement>(response)).EnumerateArray().ToList();
    }

    private static List<string> Numbers(IEnumerable<JsonElement> items) => [.. items.Select(i => i.GetProperty("number").GetString()!)];

    [Fact]
    public async Task The_calendar_lists_a_visible_scheduled_change_and_hides_an_out_of_scope_one()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var mine = await DeskScheduledAsync(world, Draft("Benim", Day(8, 8), Day(8, 10)));
        var hidden = await ScheduledAsync(world, world.Field, world.FieldTeam.Id, world.FieldUser.Id, Draft("Gizli", Day(8, 8), Day(8, 10)));

        var asDesk = await CalendarAsync(world.Desk);
        var asAdmin = await CalendarAsync(world.Admin);

        var item = Assert.Single(asDesk);
        Assert.Equal(ChangeWorld.NumberOf(mine), item.GetProperty("number").GetString());
        Assert.Equal("Benim", item.GetProperty("title").GetString());
        Assert.Equal("scheduled", item.GetProperty("status").GetString());
        Assert.Equal("medium", item.GetProperty("risk").GetString());
        Assert.Equal(DateTimeOffset.Parse(Day(8, 8)), item.GetProperty("windowStart").GetDateTimeOffset());
        Assert.Equal(DateTimeOffset.Parse(Day(8, 10)), item.GetProperty("windowEnd").GetDateTimeOffset());
        Assert.False(item.GetProperty("hasConflict").GetBoolean());
        Assert.Contains(ChangeWorld.NumberOf(hidden), Numbers(asAdmin));
    }

    [Fact]
    public async Task Draft_and_cancelled_changes_are_not_listed()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await CreateAsync(world.Desk, world.ServiceDesk.Id, world.DeskUser.Id, Draft("Taslak", Day(8, 8), Day(8, 10)));
        var toCancel = await PendingAsync(world.Desk, world.ServiceDesk.Id, world.DeskUser.Id, Draft("Iptal", Day(9, 8), Day(9, 10)));
        await ChangeWorld.ActAsync(world.Desk, toCancel, "cancel", new { version = ChangeWorld.VersionOf(toCancel), reason = "Gereksiz" });
        var kept = await DeskScheduledAsync(world, Draft("Kalan", Day(10, 8), Day(10, 10)));

        var items = await CalendarAsync(world.Desk);

        Assert.Equal([ChangeWorld.NumberOf(kept)], Numbers(items));
    }

    [Fact]
    public async Task A_window_that_starts_before_the_range_and_ends_inside_it_is_listed_in_order()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var later = await DeskScheduledAsync(world, Draft("Sonra", Day(5, 8), Day(5, 10)));
        var across = await DeskScheduledAsync(world, Draft("Sınırda", "2026-11-30T22:00:00Z", Day(1, 3)));
        await DeskScheduledAsync(world, Draft("Dışarıda", "2026-11-29T08:00:00Z", "2026-11-29T10:00:00Z"));

        var items = await CalendarAsync(world.Desk);

        Assert.Equal([ChangeWorld.NumberOf(across), ChangeWorld.NumberOf(later)], Numbers(items));
    }

    [Fact]
    public async Task Has_conflict_is_true_for_two_changes_that_share_an_asset_in_overlapping_windows()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var asset = await TestData.LegacyAssetAsync(factory, "SRV-1", world.Office.Id);
        await DeskScheduledAsync(world, Draft("Birinci", Day(8, 8), Day(8, 10)), asset);
        await DeskScheduledAsync(world, Draft("İkinci", Day(8, 9), Day(8, 11)), asset);
        await DeskScheduledAsync(world, Draft("Üçüncü", Day(9, 9), Day(9, 11)));

        var items = await CalendarAsync(world.Desk);

        Assert.Equal([true, true, false], items.Select(i => i.GetProperty("hasConflict").GetBoolean()).ToList());
    }

    [Theory]
    [InlineData("2026-12-10", "2026-12-09")]
    [InlineData("2026-12-01", "2027-02-01")]
    [InlineData(null, "2026-12-09")]
    [InlineData("2026-12-01", null)]
    [InlineData("soon", "2026-12-09")]
    public async Task A_bad_range_is_a_400_with_the_range_code(string? from, string? to)
    {
        var world = await TicketWorld.CreateAsync(factory);
        var query = string.Join("&", new[] { from is null ? null : $"from={from}", to is null ? null : $"to={to}" }.OfType<string>());

        using var response = await world.Desk.GetAsync($"{CalendarPath}?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(RangeCode, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task The_longest_range_is_62_days_and_one_more_is_rejected()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var first = DateOnly.Parse(RangeFrom);

        using var longest = await world.Desk.GetAsync($"{CalendarPath}?from={RangeFrom}&to={first.AddDays(ChangeCalendarLimits.MaxDays - 1):yyyy-MM-dd}");
        using var tooLong = await world.Desk.GetAsync($"{CalendarPath}?from={RangeFrom}&to={first.AddDays(ChangeCalendarLimits.MaxDays):yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, longest.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(62, ChangeCalendarLimits.MaxDays);
    }

    [Fact]
    public async Task An_employee_gets_not_found()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var response = await world.Employee.GetAsync($"{CalendarPath}?from={RangeFrom}&to={RangeTo}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_this_week_queue_holds_scheduled_and_implementing_changes_starting_in_the_company_week()
    {
        factory.Clock.SetUtcNow(Wednesday);
        var world = await TicketWorld.CreateAsync(factory);
        // Company week: Monday 30 Nov 00:00 to Monday 7 Dec 00:00, UTC+3.
        var sunday = await DeskScheduledAsync(world, Draft("Pazar", "2026-12-06T20:30:00Z", Day(6, 22)));
        await DeskScheduledAsync(world, Draft("Pazartesi", Day(6, 21), Day(7, 1)));
        await PendingAsync(world.Desk, world.ServiceDesk.Id, world.DeskUser.Id, Draft("Bekleyen", Day(3, 8), Day(3, 10)));
        await DeskScheduledAsync(world, Draft("Geçen hafta", "2026-11-29T20:30:00Z", Day(1, 1)));

        var page = await TicketWorld.ReadAsync(world.Desk, $"{ChangeWorld.ChangesPath}?queue=this_week");

        Assert.Equal([ChangeWorld.NumberOf(sunday)], Numbers(page.GetProperty("items").EnumerateArray()));
    }
}
