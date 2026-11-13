using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Changes;

[Collection(ApiCollection.Name)]
public sealed class ChangeApprovalTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string Action(JsonElement change, string action) => ChangeWorld.PathOf(change, action);

    private static async Task<JsonElement> DetailAsync(ApiClient client, JsonElement change) =>
        (await TicketWorld.ReadAsync(client, ChangeWorld.PathOf(change))).GetProperty("detail");

    private async Task<JsonElement> SubmittedAsync(TicketWorld world, object? draft = null)
    {
        var change = await ChangeWorld.CreateAsync(world.Desk, world, draft);
        await ChangeWorld.AddTaskAsync(factory, change, ChangePhase.Implementation, world.FieldTeam.Id);
        using var response = await world.Desk.PostAsync(Action(change, "submit"), new { version = ChangeWorld.VersionOf(change) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await world.Desk.ReadJsonAsync<JsonElement>(response);
    }

    private async Task<JsonElement> ScheduledAsync(TicketWorld world)
    {
        var submitted = await SubmittedAsync(world);
        using var response = await world.Admin.PostAsync(Action(submitted, "approval/approve"), new { approvalVersion = submitted.GetProperty("approval").GetProperty("version").GetUInt32() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await world.Admin.ReadJsonAsync<JsonElement>(response);
    }

    private static object EditBody(JsonElement change, object draft) => new { version = ChangeWorld.VersionOf(change), draft, assetIds = Array.Empty<Guid>() };

    private async Task<List<ChangeApproval>> ApprovalsAsync(JsonElement change)
    {
        var id = change.GetProperty("id").GetGuid();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ChangeApprovals.AsNoTracking().Where(a => a.ChangeId == id).OrderBy(a => a.CreatedAt).ToListAsync(Ct);
    }

    [Fact]
    public async Task Submit_without_an_implementation_task_is_not_ready()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);

        using var response = await world.Desk.PostAsync(Action(change, "submit"), new { version = ChangeWorld.VersionOf(change) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("changes.not_ready", await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Submit_without_plans_is_not_ready()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world, ChangeWorld.DraftBody(implementationPlan: null));
        await ChangeWorld.AddTaskAsync(factory, change, ChangePhase.Implementation, world.FieldTeam.Id);

        using var response = await world.Desk.PostAsync(Action(change, "submit"), new { version = ChangeWorld.VersionOf(change) });

        Assert.Equal("changes.not_ready", await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Ready_submit_gives_pending_approval_with_one_pending_row_and_asks_the_admins()
    {
        var world = await TicketWorld.CreateAsync(factory);

        var submitted = await SubmittedAsync(world);

        Assert.Equal("pending_approval", submitted.GetProperty("status").GetString());
        Assert.Equal("pending", submitted.GetProperty("approval").GetProperty("status").GetString());
        Assert.Equal(ChangeApprovalStatus.Pending, Assert.Single(await ApprovalsAsync(submitted)).Status);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mail = await db.OutboxEvents.SingleAsync(e => e.EventType == NotificationEventTypes.ChangeApprovalRequested, Ct);
        Assert.Equal(OutboxSubjectTypes.Change, mail.SubjectType);
        Assert.DoesNotContain(OutboxEvent.ApproverIdKey, mail.Payload);
    }

    [Fact]
    public async Task Cancelled_implementation_task_does_not_count_towards_ready()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await ChangeWorld.AddTaskAsync(factory, change, ChangePhase.Implementation, world.FieldTeam.Id);
        var id = change.GetProperty("id").GetGuid();
        await TestData.SaveAsync(factory, db => db.WorkTasks.Single(t => t.ChangeId == id).Cancel("Gerekmedi", DateTimeOffset.UtcNow));

        using var response = await world.Desk.PostAsync(Action(change, "submit"), new { version = ChangeWorld.VersionOf(change) });

        Assert.Equal("changes.not_ready", await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Approve_schedules_and_notifies_author_and_owner()
    {
        var world = await TicketWorld.CreateAsync(factory);

        var scheduled = await ScheduledAsync(world);

        Assert.Equal("scheduled", scheduled.GetProperty("status").GetString());
        var approval = scheduled.GetProperty("approval");
        Assert.Equal("approved", approval.GetProperty("status").GetString());
        Assert.Equal(world.AdminUser.Id, approval.GetProperty("decidedById").GetGuid());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.OutboxEvents.CountAsync(e => e.EventType == NotificationEventTypes.ChangeApprovalDecided, Ct));
        Assert.Contains(ChangeEventKind.Approved, await db.ChangeEvents.Select(e => e.Kind).ToListAsync(Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == Tgysm.Domain.Audit.AuditActions.ChangeApprovalDecided, Ct));
    }

    [Theory]
    [InlineData("reject", "rejected")]
    [InlineData("return", "returned")]
    public async Task Reject_and_return_need_a_reason_and_go_back_to_draft(string action, string expected)
    {
        var world = await TicketWorld.CreateAsync(factory);
        var submitted = await SubmittedAsync(world);
        var version = submitted.GetProperty("approval").GetProperty("version").GetUInt32();

        using var missing = await world.Admin.PostAsync(Action(submitted, $"approval/{action}"), new { approvalVersion = version });
        using var ok = await world.Admin.PostAsync(Action(submitted, $"approval/{action}"), new { approvalVersion = version, reason = "Plan eksik" });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var detail = await world.Admin.ReadJsonAsync<JsonElement>(ok);
        Assert.Equal("draft", detail.GetProperty("status").GetString());
        Assert.Equal(expected, detail.GetProperty("approval").GetProperty("status").GetString());
        Assert.Equal("Plan eksik", detail.GetProperty("approval").GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Author_and_owner_cannot_decide_but_another_admin_can()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (ownerAdmin, ownerAdminUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "owner-admin@example.com");
        var (otherAdmin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "other-admin@example.com");
        var team = await TestData.TeamAsync(factory, "Yönetim", false, ownerAdminUser.Id);
        using var created = await world.Admin.PostAsync(ChangeWorld.ChangesPath, new
        {
            draft = ChangeWorld.DraftBody(),
            ownerTeamId = team.Id,
            ownerId = ownerAdminUser.Id,
            assetIds = Array.Empty<Guid>(),
        });
        var change = await world.Admin.ReadJsonAsync<JsonElement>(created);
        await ChangeWorld.AddTaskAsync(factory, change, ChangePhase.Implementation, world.FieldTeam.Id);
        using var submit = await world.Admin.PostAsync(Action(change, "submit"), new { version = ChangeWorld.VersionOf(change) });
        var submitted = await world.Admin.ReadJsonAsync<JsonElement>(submit);
        var body = new { approvalVersion = submitted.GetProperty("approval").GetProperty("version").GetUInt32() };

        using var byAuthor = await world.Admin.PostAsync(Action(submitted, "approval/approve"), body);
        using var byOwner = await ownerAdmin.PostAsync(Action(submitted, "approval/approve"), body);
        using var byOther = await otherAdmin.PostAsync(Action(submitted, "approval/approve"), body);

        Assert.Equal(HttpStatusCode.Forbidden, byAuthor.StatusCode);
        Assert.Equal("changes.self_approval", await ApiClient.ReadCodeAsync(byAuthor));
        Assert.Equal(HttpStatusCode.Forbidden, byOwner.StatusCode);
        Assert.Equal("changes.self_approval", await ApiClient.ReadCodeAsync(byOwner));
        Assert.Equal(HttpStatusCode.OK, byOther.StatusCode);
    }

    [Fact]
    public async Task Only_active_admin_being_the_author_leaves_no_eligible_approver()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Admin, world);
        await ChangeWorld.AddTaskAsync(factory, change, ChangePhase.Implementation, world.FieldTeam.Id);

        using var submit = await world.Admin.PostAsync(Action(change, "submit"), new { version = ChangeWorld.VersionOf(change) });

        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var detail = await DetailAsync(world.Admin, change);
        Assert.Equal("pending_approval", detail.GetProperty("status").GetString());
        Assert.False(detail.GetProperty("hasEligibleApprover").GetBoolean());
    }

    [Fact]
    public async Task Employee_and_it_staff_cannot_decide_and_see_no_pending_approvals()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var submitted = await SubmittedAsync(world);
        var version = submitted.GetProperty("approval").GetProperty("version").GetUInt32();

        foreach (var client in new[] { world.Employee, world.Desk, world.Field })
        {
            using var response = await client.PostAsync(Action(submitted, "approval/approve"), new { approvalVersion = version });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("common.forbidden", await ApiClient.ReadCodeAsync(response));
            Assert.Empty((await TicketWorld.ReadAsync(client, "/api/v1/change-approvals")).EnumerateArray());
        }
    }

    [Fact]
    public async Task Pending_list_shows_what_an_eligible_admin_may_decide()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var submitted = await SubmittedAsync(world);

        var items = await TicketWorld.ReadAsync(world.Admin, "/api/v1/change-approvals");

        var item = Assert.Single(items.EnumerateArray());
        Assert.Equal(ChangeWorld.NumberOf(submitted), item.GetProperty("changeNumber").GetString());
        Assert.Equal(ChangeWorld.ImplementationPlan, item.GetProperty("implementationPlanExcerpt").GetString());
        Assert.Equal(submitted.GetProperty("approval").GetProperty("version").GetUInt32(), item.GetProperty("version").GetUInt32());
    }

    [Fact]
    public async Task Decide_on_a_change_without_pending_approval_is_409_and_unknown_is_404()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);

        using var none = await world.Admin.PostAsync(Action(change, "approval/approve"), new { approvalVersion = 1 });
        using var unknown = await world.Admin.PostAsync($"{ChangeWorld.ChangesPath}/CHG-2026-999999/approval/approve", new { approvalVersion = 1 });

        Assert.Equal("changes.approval_not_pending", await ApiClient.ReadCodeAsync(none));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Withdraw_by_the_author_marks_withdrawn_and_returns_to_draft()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var submitted = await SubmittedAsync(world);

        using var response = await world.Desk.PostAsync(Action(submitted, "withdraw"), new { version = ChangeWorld.VersionOf(submitted) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await world.Desk.ReadJsonAsync<JsonElement>(response);
        Assert.Equal("draft", detail.GetProperty("status").GetString());
        Assert.Equal("withdrawn", detail.GetProperty("approval").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Withdraw_by_someone_else_is_403()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var submitted = await SubmittedAsync(world);

        using var response = await world.Admin.PostAsync(Action(submitted, "withdraw"), new { version = ChangeWorld.VersionOf(submitted) });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("common.forbidden", await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task Window_edit_in_scheduled_voids_the_approval_and_asks_the_approver_again()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var scheduled = await ScheduledAsync(world);
        var draft = new
        {
            title = "Sunucu güncellemesi",
            justification = "Güvenlik yaması",
            affectedService = "Dosya sunucusu",
            risk = "medium",
            impact = "high",
            windowStart = "2026-12-06T20:00:00Z",
            windowEnd = "2026-12-06T22:00:00Z",
            implementationPlan = ChangeWorld.ImplementationPlan,
            verificationPlan = "Paylaşımları dene",
            backoutPlan = ChangeWorld.BackoutPlan,
        };

        using var response = await world.Desk.PutAsync(ChangeWorld.PathOf(scheduled), EditBody(scheduled, draft));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await world.Desk.ReadJsonAsync<JsonElement>(response);
        Assert.Equal("pending_approval", detail.GetProperty("status").GetString());
        Assert.Equal("pending", detail.GetProperty("approval").GetProperty("status").GetString());
        var rows = await ApprovalsAsync(scheduled);
        Assert.Equal([ChangeApprovalStatus.Void, ChangeApprovalStatus.Pending], rows.Select(r => r.Status));
        Assert.NotEqual(rows[0].Fingerprint, rows[1].Fingerprint);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var requests = await db.OutboxEvents.Where(e => e.EventType == NotificationEventTypes.ChangeApprovalRequested).OrderBy(e => e.CreatedAt).ToListAsync(Ct);
        Assert.Equal(2, requests.Count);
        Assert.Contains(world.AdminUser.Id.ToString(), requests[1].Payload);
        Assert.Contains(ChangeEventKind.ApprovalVoided, await db.ChangeEvents.Select(e => e.Kind).ToListAsync(Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == Tgysm.Domain.Audit.AuditActions.ChangeApprovalVoided, Ct));
    }

    [Fact]
    public async Task Title_edit_in_scheduled_keeps_the_approval()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var scheduled = await ScheduledAsync(world);

        using var response = await world.Desk.PutAsync(ChangeWorld.PathOf(scheduled), EditBody(scheduled, ChangeWorld.DraftBody("Yeni başlık")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await world.Desk.ReadJsonAsync<JsonElement>(response);
        Assert.Equal("scheduled", detail.GetProperty("status").GetString());
        Assert.Equal("approved", detail.GetProperty("approval").GetProperty("status").GetString());
        Assert.Single(await ApprovalsAsync(scheduled));
    }

    [Fact]
    public async Task Edit_while_pending_refreshes_the_fingerprint_so_an_old_decision_is_409()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var submitted = await SubmittedAsync(world);
        var oldVersion = submitted.GetProperty("approval").GetProperty("version").GetUInt32();
        var before = (await ApprovalsAsync(submitted)).Single().Fingerprint;

        using var edit = await world.Desk.PutAsync(ChangeWorld.PathOf(submitted), EditBody(submitted, ChangeWorld.DraftBody(implementationPlan: "Başka plan")));
        using var stale = await world.Admin.PostAsync(Action(submitted, "approval/approve"), new { approvalVersion = oldVersion });

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency.conflict", await ApiClient.ReadCodeAsync(stale));
        var row = Assert.Single(await ApprovalsAsync(submitted));
        Assert.Equal(ChangeApprovalStatus.Pending, row.Status);
        Assert.NotEqual(before, row.Fingerprint);
    }

    [Fact]
    public async Task Title_edit_while_pending_keeps_the_approval_version()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var submitted = await SubmittedAsync(world);
        var version = submitted.GetProperty("approval").GetProperty("version").GetUInt32();

        using var edit = await world.Desk.PutAsync(ChangeWorld.PathOf(submitted), EditBody(submitted, ChangeWorld.DraftBody("Yeni başlık")));
        using var approve = await world.Admin.PostAsync(Action(submitted, "approval/approve"), new { approvalVersion = version });

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
    }

    private static object WindowEditDraft(string start, string end) => new
    {
        title = "Sunucu güncellemesi",
        justification = "Güvenlik yaması",
        affectedService = "Dosya sunucusu",
        risk = "medium",
        impact = "high",
        windowStart = start,
        windowEnd = end,
        implementationPlan = ChangeWorld.ImplementationPlan,
        verificationPlan = "Paylaşımları dene",
        backoutPlan = ChangeWorld.BackoutPlan,
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Admin_who_edited_the_plan_cannot_approve_it_but_another_admin_can(bool whileScheduled)
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (editor, editorUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "editor-admin@example.com");
        var (other, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "other-admin@example.com");
        var team = world.ServiceDesk.Id;
        await TestData.SaveAsync(factory, db => db.TeamMemberships.Add(Tgysm.Domain.Organization.TeamMembership.Create(team, editorUser.Id, DateTimeOffset.UtcNow)));
        var current = whileScheduled ? await ScheduledAsync(world) : await SubmittedAsync(world);

        using var edit = await editor.PutAsync(ChangeWorld.PathOf(current), EditBody(current, WindowEditDraft("2026-12-07T20:00:00Z", "2026-12-07T22:00:00Z")));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var edited = await editor.ReadJsonAsync<JsonElement>(edit);
        var body = new { approvalVersion = edited.GetProperty("approval").GetProperty("version").GetUInt32() };

        using var self = await editor.PostAsync(Action(edited, "approval/approve"), body);
        using var fine = await other.PostAsync(Action(edited, "approval/approve"), body);

        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        Assert.Equal("changes.self_approval", await ApiClient.ReadCodeAsync(self));
        Assert.Equal(HttpStatusCode.OK, fine.StatusCode);
    }

    [Fact]
    public async Task Plan_editor_is_not_listed_and_is_not_an_eligible_approver()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (editor, editorUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "editor-admin@example.com");
        await TestData.SaveAsync(factory, db => db.TeamMemberships.Add(Tgysm.Domain.Organization.TeamMembership.Create(world.ServiceDesk.Id, editorUser.Id, DateTimeOffset.UtcNow)));
        var submitted = await SubmittedAsync(world);
        using var edit = await editor.PutAsync(ChangeWorld.PathOf(submitted), EditBody(submitted, WindowEditDraft("2026-12-07T20:00:00Z", "2026-12-07T22:00:00Z")));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        Assert.Empty((await TicketWorld.ReadAsync(editor, "/api/v1/change-approvals")).EnumerateArray());
        Assert.Single((await TicketWorld.ReadAsync(world.Admin, "/api/v1/change-approvals")).EnumerateArray());
        Assert.False((await DetailAsync(editor, submitted)).GetProperty("approval").GetProperty("canDecide").GetBoolean());
    }

    private async Task<(ApiClient Client, User User)> TeamAdminAsync(TicketWorld world, string email)
    {
        var (client, user) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, email);
        await TestData.SaveAsync(factory, db => db.TeamMemberships.Add(Tgysm.Domain.Organization.TeamMembership.Create(world.ServiceDesk.Id, user.Id, DateTimeOffset.UtcNow)));
        return (client, user);
    }

    private static async Task EditWindowAsync(ApiClient client, JsonElement change, string start, string end)
    {
        var current = await DetailAsync(client, change);
        using var edit = await client.PutAsync(ChangeWorld.PathOf(change), EditBody(current, WindowEditDraft(start, end)));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
    }

    [Fact]
    public async Task Every_plan_editor_of_the_cycle_is_excluded_not_only_the_last_one()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (a, _) = await TeamAdminAsync(world, "admin-a@example.com");
        var (b, _) = await TeamAdminAsync(world, "admin-b@example.com");
        var (c, _) = await TeamAdminAsync(world, "admin-c@example.com");
        var submitted = await SubmittedAsync(world);
        await EditWindowAsync(a, submitted, "2026-12-07T20:00:00Z", "2026-12-07T22:00:00Z");
        await EditWindowAsync(b, submitted, "2026-12-07T21:00:00Z", "2026-12-07T23:00:00Z");
        var current = await DetailAsync(c, submitted);
        var body = new { approvalVersion = current.GetProperty("approval").GetProperty("version").GetUInt32() };

        using var byA = await a.PostAsync(Action(current, "approval/approve"), body);
        using var byB = await b.PostAsync(Action(current, "approval/approve"), body);
        Assert.Equal("changes.self_approval", await ApiClient.ReadCodeAsync(byA));
        Assert.Equal("changes.self_approval", await ApiClient.ReadCodeAsync(byB));
        Assert.Equal(HttpStatusCode.Forbidden, byA.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byB.StatusCode);
        foreach (var editor in new[] { a, b })
        {
            Assert.DoesNotContain(
                (await TicketWorld.ReadAsync(editor, "/api/v1/change-approvals")).EnumerateArray(),
                item => item.GetProperty("number").GetString() == current.GetProperty("number").GetString());
            Assert.False((await DetailAsync(editor, submitted)).GetProperty("approval").GetProperty("canDecide").GetBoolean());
        }

        using var byC = await c.PostAsync(Action(current, "approval/approve"), body);
        Assert.Equal(HttpStatusCode.OK, byC.StatusCode);
    }

    [Fact]
    public async Task A_void_starts_the_new_cycle_with_only_the_voiding_user_as_editor()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (x, xUser) = await TeamAdminAsync(world, "admin-x@example.com");
        var (a, aUser) = await TeamAdminAsync(world, "admin-a@example.com");
        var submitted = await SubmittedAsync(world);
        await EditWindowAsync(x, submitted, "2026-12-07T20:00:00Z", "2026-12-07T22:00:00Z");
        var pending = await DetailAsync(world.Admin, submitted);
        using var approve = await world.Admin.PostAsync(Action(pending, "approval/approve"), new { approvalVersion = pending.GetProperty("approval").GetProperty("version").GetUInt32() });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        await EditWindowAsync(a, submitted, "2026-12-08T20:00:00Z", "2026-12-08T22:00:00Z");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var changeId = submitted.GetProperty("id").GetGuid();
        var editors = await db.ChangeApprovalEditors.AsNoTracking()
            .Where(e => db.ChangeApprovals.Any(p => p.Id == e.ApprovalId && p.ChangeId == changeId && p.Status == ChangeApprovalStatus.Pending))
            .Select(e => e.UserId).ToListAsync(Ct);
        Assert.Equal([aUser.Id], editors);
        var voided = await DetailAsync(x, submitted);
        using var byX = await x.PostAsync(Action(voided, "approval/approve"), new { approvalVersion = voided.GetProperty("approval").GetProperty("version").GetUInt32() });
        Assert.Equal(HttpStatusCode.OK, byX.StatusCode);
        Assert.NotEqual(xUser.Id, aUser.Id);
    }

    [Fact]
    public async Task Change_has_no_eligible_approver_when_the_only_other_admins_all_edited_the_plan()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var (b, _) = await TeamAdminAsync(world, "admin-b@example.com");
        var submitted = await SubmittedAsync(world);
        Assert.True((await DetailAsync(world.Desk, submitted)).GetProperty("hasEligibleApprover").GetBoolean());
        await EditWindowAsync(world.Admin, submitted, "2026-12-07T20:00:00Z", "2026-12-07T22:00:00Z");
        await EditWindowAsync(b, submitted, "2026-12-07T21:00:00Z", "2026-12-07T23:00:00Z");

        Assert.False((await DetailAsync(world.Desk, submitted)).GetProperty("hasEligibleApprover").GetBoolean());
    }

    [Fact]
    public async Task Clearing_a_plan_of_a_pending_change_is_not_ready_and_nothing_is_saved()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var submitted = await SubmittedAsync(world);

        using var response = await world.Desk.PutAsync(ChangeWorld.PathOf(submitted), EditBody(submitted, ChangeWorld.DraftBody(implementationPlan: null)));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("changes.not_ready", await ApiClient.ReadCodeAsync(response));
        Assert.Equal(ChangeWorld.ImplementationPlan, (await DetailAsync(world.Desk, submitted)).GetProperty("implementationPlan").GetString());
    }

    [Fact]
    public async Task Clearing_the_window_of_a_scheduled_change_is_not_ready()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var scheduled = await ScheduledAsync(world);
        var draft = new
        {
            title = "Sunucu güncellemesi",
            risk = "medium",
            impact = "high",
            windowStart = (string?)null,
            windowEnd = (string?)null,
            implementationPlan = ChangeWorld.ImplementationPlan,
            verificationPlan = "Paylaşımları dene",
            backoutPlan = ChangeWorld.BackoutPlan,
        };

        using var response = await world.Desk.PutAsync(ChangeWorld.PathOf(scheduled), EditBody(scheduled, draft));

        Assert.Equal("changes.not_ready", await ApiClient.ReadCodeAsync(response));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("2026-12-05T22:00:00Z", "2026-12-05T22:00:00Z")]
    [InlineData("2026-12-05T22:00:00Z", "2026-12-05T20:00:00Z")]
    public async Task Submit_without_a_window_or_with_an_end_not_after_the_start_is_not_ready(string? start, string? end)
    {
        var world = await TicketWorld.CreateAsync(factory);
        var draft = new
        {
            title = "Sunucu güncellemesi",
            risk = "medium",
            impact = "high",
            windowStart = start,
            windowEnd = end,
            implementationPlan = ChangeWorld.ImplementationPlan,
            verificationPlan = "Paylaşımları dene",
            backoutPlan = ChangeWorld.BackoutPlan,
        };
        var change = await ChangeWorld.CreateAsync(world.Desk, world, draft);
        await ChangeWorld.AddTaskAsync(factory, change, ChangePhase.Implementation, world.FieldTeam.Id);

        using var response = await world.Desk.PostAsync(Action(change, "submit"), new { version = ChangeWorld.VersionOf(change) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("changes.not_ready", await ApiClient.ReadCodeAsync(response));
    }
}
