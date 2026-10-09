using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Assets;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Tickets;

[Collection(ApiCollection.Name)]
public sealed class IncidentAssetTests(ApiFactory factory) : IAsyncLifetime
{
    private const string IncidentsPath = "/api/v1/tickets/incidents";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<JsonElement> AssetAsync(TicketWorld world, Guid? assignTo)
    {
        var store = await TestData.LocationAsync(factory, $"Depo {Guid.NewGuid():N}");
        var asset = await AssetCatalogueTests.CreateAsync(world.Desk, new { modelId = await TestData.ModelAsync(factory), locationId = store.Id });
        if (assignTo is { } user)
        {
            using var assign = await world.Desk.PostAsync($"{AssetCatalogueTests.AssetsPath}/{asset.GetProperty("id").GetGuid()}/assign", new { version = asset.GetProperty("version").GetUInt32(), userId = user });
            Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        }

        return asset;
    }

    [Fact]
    public async Task An_incident_for_my_assigned_device_is_linked_to_it()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var asset = await AssetAsync(world, world.EmployeeUser.Id);
        var assetId = asset.GetProperty("id").GetGuid();

        using var response = await world.Employee.PostAsync(IncidentsPath, new { subject = "Ekran kırık", description = "Açılmıyor", assetId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var ticket = await world.Employee.ReadJsonAsync<JsonElement>(response);
        await using var db = factory.Database.CreateDbContext();
        Assert.True(await db.TicketAssets.AnyAsync(l => l.AssetId == assetId && l.TicketId == ticket.GetProperty("id").GetGuid(), Ct));
    }

    [Fact]
    public async Task The_device_chosen_on_a_new_incident_appears_in_its_timeline_and_audit_like_a_later_link()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var assetId = (await AssetAsync(world, world.EmployeeUser.Id)).GetProperty("id").GetGuid();

        using var response = await world.Employee.PostAsync(IncidentsPath, new { subject = "Ekran kırık", description = "Açılmıyor", assetId });
        var ticketId = (await world.Employee.ReadJsonAsync<JsonElement>(response)).GetProperty("id").GetGuid();

        await using var db = factory.Database.CreateDbContext();
        var linked = await db.TicketEvents.SingleAsync(e => e.TicketId == ticketId && e.Kind == TicketEventKind.AssetLinked, Ct);
        Assert.Equal(world.EmployeeUser.Id, linked.ActorUserId);
        Assert.Contains(assetId.ToString(), linked.DataJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.TicketAssetLinked && a.EntityId == ticketId, Ct));
        // The timeline reads "created", then "device linked" (same instant; ordered like the timeline query).
        var order = await db.TicketEvents.Where(e => e.TicketId == ticketId).OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).Select(e => e.Kind).ToListAsync(Ct);
        Assert.Equal([TicketEventKind.Created, TicketEventKind.AssetLinked], order.Take(2));
    }

    [Fact]
    public async Task A_device_that_is_not_assigned_to_me_is_refused_and_no_ticket_is_created()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var other = await TestUsers.CreateAsync(factory, UserRole.Employee, "baska@example.com");
        var someoneElses = await AssetAsync(world, other.Id);
        var inStock = await AssetAsync(world, assignTo: null);

        using var foreign = await world.Employee.PostAsync(IncidentsPath, new { subject = "Ekran", description = "x", assetId = someoneElses.GetProperty("id").GetGuid() });
        using var stock = await world.Employee.PostAsync(IncidentsPath, new { subject = "Ekran", description = "x", assetId = inStock.GetProperty("id").GetGuid() });

        Assert.Equal(ErrorCodes.TicketAssetNotAssignedToRequester, await ApiClient.ReadCodeAsync(foreign));
        Assert.Equal(ErrorCodes.TicketAssetNotAssignedToRequester, await ApiClient.ReadCodeAsync(stock));
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(0, await db.Tickets.CountAsync(Ct));
    }
}
