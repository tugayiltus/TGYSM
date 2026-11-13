using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Notifications;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Notifications;

[Collection(ApiCollection.Name)]
public sealed class ChangeNotificationTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);
    private int _emailCounter;
    private User _author = null!;
    private User _owner = null!;
    private Team _team = null!;
    private ChangeRequest _change = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        _author = await AddUserAsync(UserRole.Admin);
        _owner = await AddUserAsync(UserRole.Admin);
        _team = Team.Create("Ağ");
        var draft = new ChangeDraft("Anahtar güncelle", null, "Ağ", Level.High, Level.Medium, Now.AddDays(1), Now.AddDays(1).AddHours(2), null, null, null);
        _change = ChangeRequest.Create("CHG-2026-000001", draft, _team.Id, _owner.Id, _author.Id, Now);
        await using var db = factory.Database.CreateDbContext();
        db.Teams.Add(_team);
        db.ChangeRequests.Add(_change);
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<User> AddUserAsync(UserRole role, bool active = true)
    {
        var user = User.Create("Kişi", $"No{++_emailCounter}", $"user{_emailCounter}@example.com", role, null, null, "hash", Now);
        if (!active)
        {
            user.Deactivate(Now);
        }

        await using var db = factory.Database.CreateDbContext();
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        return user;
    }

    private async Task EmitAsync(string eventType, Guid? actorId = null, Guid? approverId = null)
    {
        var payload = new Dictionary<string, string>();
        if (actorId is { } actor)
        {
            payload[OutboxEvent.ActorIdKey] = actor.ToString();
        }

        if (approverId is { } approver)
        {
            payload[OutboxEvent.ApproverIdKey] = approver.ToString();
        }

        await using (var db = factory.Database.CreateDbContext())
        {
            db.OutboxEvents.Add(OutboxEvent.CreateForSubject(eventType, OutboxSubjectTypes.Change, _change.Id, null, JsonSerializer.Serialize(payload), Now));
            await db.SaveChangesAsync(Ct);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DispatchOutboxHandler>().HandleAsync(50, Lease, Ct);
    }

    private async Task<List<Guid>> RecipientsAsync()
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.EmailDeliveries.Select(d => d.RecipientUserId).OrderBy(id => id).ToListAsync(Ct);
    }

    private static List<Guid> Sorted(params Guid[] ids) => [.. ids.Order()];

    [Fact]
    public async Task Approval_request_goes_to_every_active_admin_except_author_and_owner()
    {
        var first = await AddUserAsync(UserRole.Admin);
        var second = await AddUserAsync(UserRole.Admin);
        await AddUserAsync(UserRole.Admin, active: false);
        await AddUserAsync(UserRole.ItStaff);

        await EmitAsync(NotificationEventTypes.ChangeApprovalRequested, _author.Id);

        Assert.Equal(Sorted(first.Id, second.Id), await RecipientsAsync());
        await using var db = factory.Database.CreateDbContext();
        var delivery = await db.EmailDeliveries.FirstAsync(Ct);
        Assert.Contains("CHG-2026-000001", delivery.Subject, StringComparison.Ordinal);
        Assert.Contains("Anahtar güncelle", delivery.Body, StringComparison.Ordinal);
        Assert.Contains($"{ApiFactory.PublicBaseUrl}/changes/CHG-2026-000001", delivery.Body, StringComparison.Ordinal);
        Assert.Contains("14.11.2026", delivery.Body, StringComparison.Ordinal);
        Assert.Contains("Yüksek", delivery.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", delivery.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Approval_request_skips_every_plan_editor_of_the_pending_approval()
    {
        var editorA = await AddUserAsync(UserRole.Admin);
        var editorB = await AddUserAsync(UserRole.Admin);
        var eligible = await AddUserAsync(UserRole.Admin);
        var approval = ChangeApproval.Request(_change.Id, new string('a', ChangeApproval.FingerprintLength), Now);
        await using (var db = factory.Database.CreateDbContext())
        {
            db.ChangeApprovals.Add(approval);
            db.ChangeApprovalEditors.Add(ChangeApprovalEditor.Create(approval.Id, _author.Id));
            db.ChangeApprovalEditors.Add(ChangeApprovalEditor.Create(approval.Id, editorA.Id));
            db.ChangeApprovalEditors.Add(ChangeApprovalEditor.Create(approval.Id, editorB.Id));
            await db.SaveChangesAsync(Ct);
        }

        await EmitAsync(NotificationEventTypes.ChangeApprovalRequested, _author.Id);

        Assert.Equal([eligible.Id], await RecipientsAsync());
    }

    [Fact]
    public async Task Approval_request_after_a_void_goes_only_to_the_named_admin()
    {
        var approver = await AddUserAsync(UserRole.Admin);
        await AddUserAsync(UserRole.Admin);

        await EmitAsync(NotificationEventTypes.ChangeApprovalRequested, _author.Id, approver.Id);

        Assert.Equal([approver.Id], await RecipientsAsync());
    }

    [Fact]
    public async Task Approval_request_after_a_void_falls_back_to_eligible_admins_when_the_named_approver_voided_it()
    {
        var voider = await AddUserAsync(UserRole.Admin);
        var eligible = await AddUserAsync(UserRole.Admin);
        var approval = ChangeApproval.Request(_change.Id, new string('a', ChangeApproval.FingerprintLength), Now);
        await using (var db = factory.Database.CreateDbContext())
        {
            db.ChangeApprovals.Add(approval);
            db.ChangeApprovalEditors.Add(ChangeApprovalEditor.Create(approval.Id, voider.Id));
            await db.SaveChangesAsync(Ct);
        }

        await EmitAsync(NotificationEventTypes.ChangeApprovalRequested, voider.Id, voider.Id);

        Assert.Equal([eligible.Id], await RecipientsAsync());
    }

    [Fact]
    public async Task Approval_decision_goes_to_author_and_owner_but_not_the_actor()
    {
        // The owner (an admin who is not the author) decides: only the author is mailed.
        await EmitAsync(NotificationEventTypes.ChangeApprovalDecided, _owner.Id);

        Assert.Equal([_author.Id], await RecipientsAsync());
    }

    [Fact]
    public async Task Approval_decision_reaches_both_when_a_third_admin_decides()
    {
        var decider = await AddUserAsync(UserRole.Admin);

        await EmitAsync(NotificationEventTypes.ChangeApprovalDecided, decider.Id);

        Assert.Equal(Sorted(_author.Id, _owner.Id), await RecipientsAsync());
    }

    [Fact]
    public async Task Approval_decision_reaches_one_person_when_author_owns_the_change()
    {
        await using (var db = factory.Database.CreateDbContext())
        {
            await db.Database.ExecuteSqlAsync($"UPDATE change_requests SET owner_id = {_author.Id}", Ct);
        }

        await EmitAsync(NotificationEventTypes.ChangeApprovalDecided);

        Assert.Equal([_author.Id], await RecipientsAsync());
    }

    [Fact]
    public async Task Backout_start_goes_to_assignees_else_team_members_and_skips_other_phases()
    {
        var assignee = await AddUserAsync(UserRole.ItStaff);
        var member = await AddUserAsync(UserRole.ItStaff);
        var inactiveMember = await AddUserAsync(UserRole.ItStaff, active: false);
        var preparer = await AddUserAsync(UserRole.ItStaff);
        var actor = await AddUserAsync(UserRole.Admin);
        var other = Team.Create("Sunucu");
        await using (var db = factory.Database.CreateDbContext())
        {
            db.Teams.Add(other);
            await db.SaveChangesAsync(Ct);
            db.TeamMemberships.AddRange(
                TeamMembership.Create(other.Id, member.Id, Now),
                TeamMembership.Create(other.Id, inactiveMember.Id, Now),
                TeamMembership.Create(other.Id, actor.Id, Now));
            db.WorkTasks.AddRange(
                BackoutTask("TSK-2026-000001", _team.Id, assignee.Id, ChangePhase.Backout),
                BackoutTask("TSK-2026-000002", other.Id, null, ChangePhase.Backout),
                BackoutTask("TSK-2026-000003", _team.Id, preparer.Id, ChangePhase.Preparation));
            await db.SaveChangesAsync(Ct);
        }

        await EmitAsync(NotificationEventTypes.ChangeBackoutStarted, actor.Id);

        Assert.Equal(Sorted(assignee.Id, member.Id), await RecipientsAsync());
    }

    private WorkTask BackoutTask(string number, Guid teamId, Guid? assigneeId, ChangePhase phase) =>
        WorkTask.Create(number, "Geri al", null, teamId, assigneeId, Priority.P2, null, null, _change.Id, phase, _author.Id, Now);
}
