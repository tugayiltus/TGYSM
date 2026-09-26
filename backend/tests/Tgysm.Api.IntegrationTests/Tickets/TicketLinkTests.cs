using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Assets;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;

namespace Tgysm.Api.IntegrationTests.Tickets;

[Collection(ApiCollection.Name)]
public sealed class TicketLinkTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<JsonElement> AssetAsync(TicketWorld world, string tag, Guid? assignTo = null)
    {
        var store = await TestData.LocationAsync(world.Factory, $"Depo {tag}");
        var asset = await AssetCatalogueTests.CreateAsync(world.Desk, new { assetTag = tag, type = "laptop", brand = "Lenovo", model = "T14", locationId = store.Id });
        if (assignTo is { } user)
        {
            using var assign = await world.Desk.PostAsync($"{AssetCatalogueTests.AssetsPath}/{asset.GetProperty("id").GetGuid()}/assign", new { version = asset.GetProperty("version").GetUInt32(), userId = user });
            Assert.Equal(HttpStatusCode.OK, assign.StatusCode);
        }

        return asset;
    }

    [Fact]
    public async Task Related_request_copies_public_data_links_both_ways_and_starts_its_approval()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee, extra: new { subject = "Dizüstü açılmıyor", description = "Anakart arızası", locationId = world.Office.Id });
        using var _ = await world.Employee.PostMultipartAsync(TicketWorld.PathOf(incident, "comments"), new Dictionary<string, string> { ["body"] = "Gizli değerlendirme", ["visibility"] = "public" }, []);
        using var note = await world.Desk.PostMultipartAsync(TicketWorld.PathOf(incident, "comments"), new Dictionary<string, string> { ["body"] = "İç not", ["visibility"] = "internal" }, []);

        using var create = await world.Desk.PostAsync(TicketWorld.PathOf(incident, "related-request"), new { requestTypeId = world.NewLaptop.Id });
        var request = await world.Desk.ReadJsonAsync<JsonElement>(create);
        var incidentView = await TicketWorld.ReadAsync(world.Employee, TicketWorld.PathOf(incident));
        var requestComments = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(request, "comments"));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(("Dizüstü açılmıyor", "Anakart arızası", "Ayşe Kaya"),
            (request.GetProperty("subject").GetString(), request.GetProperty("description").GetString(), request.GetProperty("requesterName").GetString()));
        Assert.Equal(world.Office.Id, request.GetProperty("locationId").GetGuid());
        Assert.Equal("pending", request.GetProperty("approval").GetProperty("status").GetString());
        Assert.Equal(0, requestComments.GetArrayLength());
        Assert.Equal(TicketWorld.NumberOf(request), incidentView.GetProperty("relatedTickets")[0].GetProperty("number").GetString());
        Assert.Equal(TicketWorld.NumberOf(incident), request.GetProperty("relatedTickets")[0].GetProperty("number").GetString());

        var completed = await world.Desk.PostAsync(TicketWorld.PathOf(incident, "complete"), new { version = TicketWorld.VersionOf(incidentView), resolution = "Yeni cihaz talebi açıldı", outcome = "service_request_required" });
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
    }

    [Fact]
    public async Task Only_incidents_get_related_requests_and_employees_cannot_create_them()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        var request = await world.CreateRequestAsync(world.Employee, approval: false);

        using var employee = await world.Employee.PostAsync(TicketWorld.PathOf(incident, "related-request"), new { requestTypeId = world.Software.Id });
        using var fromRequest = await world.Desk.PostAsync(TicketWorld.PathOf(request, "related-request"), new { requestTypeId = world.Software.Id });

        Assert.Equal(HttpStatusCode.NotFound, employee.StatusCode);
        Assert.Equal(ErrorCodes.TicketInvalidTransition, await ApiClient.ReadCodeAsync(fromRequest));
    }

    [Fact]
    public async Task Linked_assets_are_visible_from_both_sides_within_scope()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var asset = await AssetAsync(world, "DT-1");
        var assetId = asset.GetProperty("id").GetGuid();

        using var link = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "assets"), new { assetId });
        using var duplicate = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "assets"), new { assetId });
        var ticketAssets = await TicketWorld.ReadAsync(world.Employee, TicketWorld.PathOf(ticket, "assets"));
        var deskTickets = await TicketWorld.ReadAsync(world.Desk, $"{AssetCatalogueTests.AssetsPath}/{assetId}/tickets");
        var fieldTickets = await TicketWorld.ReadAsync(world.Field, $"{AssetCatalogueTests.AssetsPath}/{assetId}/tickets");
        using var employeeAssetTickets = await world.Employee.GetAsync($"{AssetCatalogueTests.AssetsPath}/{assetId}/tickets");
        using var unlink = await world.Desk.DeleteAsync(TicketWorld.PathOf(ticket, $"assets/{assetId}"));

        Assert.Equal(HttpStatusCode.OK, link.StatusCode);
        Assert.Equal(ErrorCodes.Duplicate, await ApiClient.ReadCodeAsync(duplicate));
        Assert.Equal("DT-1", ticketAssets[0].GetProperty("assetTag").GetString());
        Assert.Equal(TicketWorld.NumberOf(ticket), deskTickets[0].GetProperty("number").GetString());
        Assert.Equal(0, fieldTickets.GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, employeeAssetTickets.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unlink.StatusCode);
        Assert.Equal(0, (await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(ticket, "assets"))).GetArrayLength());
    }

    [Fact]
    public async Task Employee_links_only_an_asset_assigned_to_them()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var mine = await AssetAsync(world, "DT-1", world.EmployeeUser.Id);
        var other = await AssetAsync(world, "DT-2");

        using var own = await world.Employee.PostAsync(TicketWorld.PathOf(ticket, "assets"), new { assetId = mine.GetProperty("id").GetGuid() });
        using var foreign = await world.Employee.PostAsync(TicketWorld.PathOf(ticket, "assets"), new { assetId = other.GetProperty("id").GetGuid() });

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
