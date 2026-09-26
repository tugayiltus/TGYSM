using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.IntegrationTests.Tickets;

[Collection(ApiCollection.Name)]
public sealed class TicketCreationTests(ApiFactory factory) : IAsyncLifetime
{
    private const int ParallelCreators = 6;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Incident_gets_a_number_the_intake_team_and_p3()
    {
        var world = await TicketWorld.CreateAsync(factory);

        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee, extra: new
        {
            subject = " Yazıcı çalışmıyor ",
            description = "Kağıt sıkışıyor",
            categoryId = world.Hardware.Id,
            locationId = world.Office.Id,
            priority = "p1",
        });

        Assert.Equal($"INC-{DateTimeOffset.UtcNow.Year}-000001", TicketWorld.NumberOf(ticket));
        Assert.Equal("Yazıcı çalışmıyor", ticket.GetProperty("subject").GetString());
        Assert.Equal("open", ticket.GetProperty("status").GetString());
        Assert.Equal("p3", ticket.GetProperty("priority").GetString());
        Assert.Equal("Servis Masası", ticket.GetProperty("teamName").GetString());
        Assert.Equal("Donanım", ticket.GetProperty("categoryName").GetString());
        Assert.Equal("Ayşe Kaya", ticket.GetProperty("requesterName").GetString());
        await using var db = factory.Database.CreateDbContext();
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.TicketCreated, Ct));
        Assert.Equal(TicketEventKind.Created, (await db.TicketEvents.SingleAsync(Ct)).Kind);
    }

    [Fact]
    public async Task Creation_without_an_intake_team_is_refused()
    {
        var world = await TicketWorld.CreateAsync(factory, withIntakeTeam: false);

        using var response = await world.Employee.PostAsync(TicketWorld.IncidentsPath, new { subject = "Ağ yok", description = "Bağlanamıyorum" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.TicketNoIntakeTeam, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Invalid_input_and_inactive_category_are_refused()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var old = await TestData.CategoryAsync(factory, "Eski", active: false);
        var requestOnly = await TestData.CategoryAsync(factory, "Kurulum", Domain.Catalog.CategoryScope.Request);

        using var empty = await world.Employee.PostAsync(TicketWorld.IncidentsPath, new { subject = " ", description = "" });
        using var inactive = await world.Employee.PostAsync(TicketWorld.IncidentsPath, new { subject = "A", description = "B", categoryId = old.Id });
        using var wrongKind = await world.Employee.PostAsync(TicketWorld.IncidentsPath, new { subject = "A", description = "B", categoryId = requestOnly.Id });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        var fields = await ApiClient.ReadFieldCodesAsync(empty);
        Assert.Contains(ErrorCodes.Required, fields["subject"]);
        Assert.Contains(ErrorCodes.Required, fields["description"]);
        Assert.Equal(ErrorCodes.ReferenceInactive, await ApiClient.ReadCodeAsync(inactive));
        Assert.Equal(ErrorCodes.ReferenceInactive, await ApiClient.ReadCodeAsync(wrongKind));
    }

    [Fact]
    public async Task Request_needing_approval_waits_for_the_department_approver()
    {
        var world = await TicketWorld.CreateAsync(factory);

        var request = await world.CreateRequestAsync(world.Employee, approval: true);
        var simple = await world.CreateRequestAsync(world.Employee, approval: false);

        Assert.StartsWith("REQ-", TicketWorld.NumberOf(request));
        Assert.Equal("on_hold", request.GetProperty("status").GetString());
        Assert.Equal("manager_approval", request.GetProperty("holdReason").GetString());
        var approval = request.GetProperty("approval");
        Assert.Equal("pending", approval.GetProperty("status").GetString());
        Assert.Equal(world.AdminUser.Id, approval.GetProperty("approverId").GetGuid());
        Assert.Equal("open", simple.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, simple.GetProperty("approval").ValueKind);
    }

    [Fact]
    public async Task Approver_requesting_for_themselves_gets_an_unassigned_approval()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TestData.SaveAsync(factory, db => db.Users.Attach(world.AdminUser).Entity.UpdateProfile(
            world.AdminUser.FirstName, world.AdminUser.LastName, world.AdminUser.Email, world.Accounting.Id, null, DateTimeOffset.UtcNow));

        var request = await world.CreateRequestAsync(world.Admin, approval: true);

        Assert.Equal(JsonValueKind.Null, request.GetProperty("approval").GetProperty("approverId").ValueKind);
    }

    [Fact]
    public async Task Request_without_department_approver_gets_an_unassigned_approval()
    {
        var world = await TicketWorld.CreateAsync(factory);

        var request = await world.CreateRequestAsync(world.Desk, approval: true);

        Assert.Equal("pending", request.GetProperty("approval").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, request.GetProperty("approval").GetProperty("approverId").ValueKind);
    }

    [Fact]
    public async Task Parallel_creation_gives_distinct_numbers()
    {
        var world = await TicketWorld.CreateAsync(factory);

        var tickets = await Task.WhenAll(Enumerable.Range(0, ParallelCreators).Select(i => TicketWorld.CreateIncidentAsync(world.Employee, $"Sorun {i}")));

        Assert.Equal(ParallelCreators, tickets.Select(TicketWorld.NumberOf).Distinct().Count());
    }
}
