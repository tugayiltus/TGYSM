using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Assets;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Tickets;

[Collection(ApiCollection.Name)]
public sealed class TicketApprovalTests(ApiFactory factory) : IAsyncLifetime
{
    private const string MyApprovalsPath = "/api/v1/approvals/mine";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static JsonElement ApprovalOf(JsonElement ticket) => ticket.GetProperty("approval");

    private static string DecisionPath(JsonElement ticket, string decision) =>
        $"/api/v1/approvals/{ApprovalOf(ticket).GetProperty("id").GetGuid()}/{decision}";

    private static object VersionBody(JsonElement ticket, string? reason = null) =>
        new { version = ApprovalOf(ticket).GetProperty("version").GetUInt32(), reason };

    [Fact]
    public async Task Approver_sees_and_approves_a_request_which_then_opens()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var request = await world.CreateRequestAsync(world.Employee, approval: true);

        var mine = await TicketWorld.ReadAsync(world.Admin, MyApprovalsPath);
        using var approve = await world.Admin.PostAsync(DecisionPath(request, "approve"), VersionBody(request));
        var after = await TicketWorld.ReadAsync(world.Employee, TicketWorld.PathOf(request));

        Assert.Equal(TicketWorld.NumberOf(request), Assert.Single(mine.EnumerateArray()).GetProperty("ticketNumber").GetString());
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        Assert.Equal("open", after.GetProperty("status").GetString());
        Assert.Equal("approved", ApprovalOf(after).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Rejection_cancels_the_request_with_the_reason()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var request = await world.CreateRequestAsync(world.Employee, approval: true);

        using var noReason = await world.Admin.PostAsync(DecisionPath(request, "reject"), VersionBody(request, " "));
        using var reject = await world.Admin.PostAsync(DecisionPath(request, "reject"), VersionBody(request, "Bütçe yok"));
        var after = await TicketWorld.ReadAsync(world.Employee, TicketWorld.PathOf(request));

        Assert.Contains(ErrorCodes.Required, (await ApiClient.ReadFieldCodesAsync(noReason))["reason"]);
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        Assert.Equal(("cancelled", "approval_rejected", "Bütçe yok"),
            (after.GetProperty("status").GetString(), after.GetProperty("outcome").GetString(), after.GetProperty("cancelReason").GetString()));
    }

    [Fact]
    public async Task Only_the_assigned_approver_decides_and_requesters_never_approve_themselves()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (otherAdmin, otherAdminUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin2@example.com");
        var request = await world.CreateRequestAsync(world.Employee, approval: true);
        var own = await world.CreateRequestAsync(otherAdmin, approval: true);

        using var notApprover = await otherAdmin.PostAsync(DecisionPath(request, "approve"), VersionBody(request));
        using var ownApproval = await otherAdmin.PostAsync(DecisionPath(own, "approve"), VersionBody(own));
        using var employee = await world.Employee.PostAsync(DecisionPath(request, "approve"), VersionBody(request));
        using var reassignToRequester = await world.Admin.PostAsync(TicketWorld.PathOf(own, "approval/reassign"), new { approverId = otherAdminUser.Id, reason = "Deneme" });

        Assert.Equal(ErrorCodes.ApprovalNotApprover, await ApiClient.ReadCodeAsync(notApprover));
        Assert.Equal(ErrorCodes.ApprovalNotApprover, await ApiClient.ReadCodeAsync(ownApproval));
        Assert.Equal(HttpStatusCode.Forbidden, employee.StatusCode);
        Assert.Equal(ErrorCodes.ApprovalApproverInvalid, await ApiClient.ReadCodeAsync(reassignToRequester));
    }

    [Fact]
    public async Task Admin_reassigns_an_unassigned_approval_with_a_reason()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var request = await world.CreateRequestAsync(world.Desk, approval: true);

        using var reassign = await world.Admin.PostAsync(TicketWorld.PathOf(request, "approval/reassign"), new { approverId = world.AdminUser.Id, reason = "Departman onaylayıcısı yok" });
        var after = await world.Admin.ReadJsonAsync<JsonElement>(reassign);
        using var approve = await world.Admin.PostAsync(DecisionPath(after, "approve"), VersionBody(after));

        Assert.Equal(world.AdminUser.Id, ApprovalOf(after).GetProperty("approverId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal("Departman onaylayıcısı yok", (await db.AuditEvents.SingleAsync(a => a.Action == AuditActions.ApprovalReassigned, Ct)).Reason);
    }

    [Fact]
    public async Task Invalidated_approval_puts_the_request_back_on_hold_with_a_new_pending_approval()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var request = await world.CreateRequestAsync(world.Employee, approval: true);
        using var _ = await world.Admin.PostAsync(DecisionPath(request, "approve"), VersionBody(request));
        var approved = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(request));

        using var invalidate = await world.Desk.PostAsync(TicketWorld.PathOf(request, "approval/invalidate"), new { version = TicketWorld.VersionOf(approved), reason = "Model değişti" });
        var after = await world.Desk.ReadJsonAsync<JsonElement>(invalidate);

        Assert.Equal(HttpStatusCode.OK, invalidate.StatusCode);
        Assert.Equal(("on_hold", "manager_approval"), (after.GetProperty("status").GetString(), after.GetProperty("holdReason").GetString()));
        Assert.Equal("pending", ApprovalOf(after).GetProperty("status").GetString());
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.Approvals.CountAsync(a => a.Status == Domain.Tickets.ApprovalStatus.Void, Ct));
    }

    [Fact]
    public async Task Unapproved_request_blocks_completion_and_asset_handover()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var request = await world.CreateRequestAsync(world.Employee, approval: true);
        var store = await TestData.LocationAsync(factory, "Depo");
        var asset = await AssetCatalogueTests.CreateAsync(world.Desk, new { assetTag = "DT-1", type = "laptop", brand = "Lenovo", model = "T14", locationId = store.Id });

        using var complete = await world.Desk.PostAsync(TicketWorld.PathOf(request, "complete"), new { version = TicketWorld.VersionOf(request), resolution = "Verildi" });
        using var handover = await world.Desk.PostAsync($"{AssetCatalogueTests.AssetsPath}/{asset.GetProperty("id").GetGuid()}/assign", new
        {
            version = asset.GetProperty("version").GetUInt32(),
            userId = world.EmployeeUser.Id,
            requestTicketId = request.GetProperty("id").GetGuid(),
        });

        Assert.Equal(ErrorCodes.TicketInvalidTransition, await ApiClient.ReadCodeAsync(complete));
        Assert.Equal(ErrorCodes.TicketApprovalRequired, await ApiClient.ReadCodeAsync(handover));
    }

    [Fact]
    public async Task Parallel_approve_and_reject_have_one_winner()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var request = await world.CreateRequestAsync(world.Employee, approval: true);

        var responses = await Task.WhenAll(
            world.Admin.PostAsync(DecisionPath(request, "approve"), VersionBody(request)),
            world.Admin.PostAsync(DecisionPath(request, "reject"), VersionBody(request, "Hayır")));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Handover_against_a_request_goes_only_to_its_requester_while_it_is_open()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var request = await world.CreateRequestAsync(world.Employee, approval: false);
        var cancelled = await world.CreateRequestAsync(world.Employee, approval: false);
        using var _ = await world.Desk.PostAsync(TicketWorld.PathOf(cancelled, "cancel"), new { version = TicketWorld.VersionOf(cancelled), reason = "Vazgeçildi" });
        var store = await TestData.LocationAsync(factory, "Depo");
        var asset = await AssetCatalogueTests.CreateAsync(world.Desk, new { assetTag = "DT-1", type = "laptop", brand = "Lenovo", model = "T14", locationId = store.Id });
        var other = await TestUsers.CreateAsync(factory, UserRole.Employee, "baska@example.com");
        var assignPath = $"{AssetCatalogueTests.AssetsPath}/{asset.GetProperty("id").GetGuid()}/assign";
        var version = asset.GetProperty("version").GetUInt32();

        using var toOther = await world.Desk.PostAsync(assignPath, new { version, userId = other.Id, requestTicketId = request.GetProperty("id").GetGuid() });
        using var toClosed = await world.Desk.PostAsync(assignPath, new { version, userId = world.EmployeeUser.Id, requestTicketId = cancelled.GetProperty("id").GetGuid() });

        Assert.Equal(ErrorCodes.AssetRequestMismatch, await ApiClient.ReadCodeAsync(toOther));
        Assert.Equal(ErrorCodes.AssetRequestMismatch, await ApiClient.ReadCodeAsync(toClosed));
    }
}
