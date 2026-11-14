using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Changes;

internal static class ChangeQueries
{
    private const string NameSeparator = " ";

    private static readonly ChangePhase[] PlanPhases = [ChangePhase.Preparation, ChangePhase.Implementation, ChangePhase.Verification];

    public static async Task<ChangeView> LoadViewAsync(
        IAppDbContext db, Guid changeId, ChangeViewer viewer, ChangeAccess access, CancellationToken cancellationToken) =>
        access switch
        {
            ChangeAccess.Full => new ChangeView(access, await LoadDetailAsync(db, changeId, viewer, cancellationToken), null),
            ChangeAccess.Summary => new ChangeView(access, null, await LoadSummaryAsync(db, changeId, viewer, cancellationToken)),
            _ => throw new NotFoundException(AuditEntityTypes.Change),
        };

    public static async Task<ChangeSummary> LoadSummaryAsync(IAppDbContext db, Guid changeId, ChangeViewer viewer, CancellationToken cancellationToken)
    {
        var change = await db.ChangeRequests.AsNoTracking().SingleAsync(c => c.Id == changeId, cancellationToken);
        var teamIds = viewer.TeamIds;
        var userId = viewer.UserId;
        var phases = await db.WorkTasks.AsNoTracking()
            .Where(t => t.ChangeId == changeId && (teamIds.Contains(t.TeamId) || t.AssigneeId == userId))
            .Select(t => t.Phase).Distinct().ToListAsync(cancellationToken);
        var sawPlan = phases.Any(p => p is { } phase && PlanPhases.Contains(phase));
        var sawBackout = phases.Contains(ChangePhase.Backout);
        return new ChangeSummary(
            change.Id, change.Number, change.Title, change.Status, change.WindowStart, change.WindowEnd, change.Risk,
            sawPlan ? change.ImplementationPlan : null,
            sawBackout ? change.BackoutPlan : null);
    }

    public static async Task<ChangeDetail> LoadDetailAsync(IAppDbContext db, Guid changeId, ChangeViewer viewer, CancellationToken cancellationToken)
    {
        var row = await db.ChangeRequests.AsNoTracking().Where(c => c.Id == changeId)
            .Select(c => new
            {
                Change = c,
                OwnerTeamName = db.Teams.Where(t => t.Id == c.OwnerTeamId).Select(t => t.Name).First(),
                OwnerName = db.Users.Where(u => u.Id == c.OwnerId).Select(u => u.FirstName + NameSeparator + u.LastName).First(),
                CreatedByName = db.Users.Where(u => u.Id == c.CreatedById).Select(u => u.FirstName + NameSeparator + u.LastName).First(),
            })
            .SingleAsync(cancellationToken);
        var change = row.Change;
        var assets = await (
            from l in db.ChangeAssets.AsNoTracking()
            join a in db.Assets on l.AssetId equals a.Id
            where l.ChangeId == changeId
            orderby a.AssetTag
            select new ChangeAssetItem(a.Id, a.AssetTag, a.Type, a.Brand, a.Model, a.Status)).ToListAsync(cancellationToken);
        var tasks = await db.WorkTasks.AsNoTracking().Where(t => t.ChangeId == changeId)
            .OrderBy(t => t.Number)
            .Select(t => new ChangeTaskRow(
                t.Id, t.Number, t.Title, t.Status, t.Phase!.Value, t.TeamId,
                db.Teams.Where(x => x.Id == t.TeamId).Select(x => x.Name).First(),
                db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault()))
            .ToListAsync(cancellationToken);
        var groups = Enum.GetValues<ChangePhase>().Select(p => new ChangePhaseGroup(p, [.. tasks.Where(t => t.Phase == p)])).ToList();
        var hasEligibleApprover = await ChangeApprovalFlow.HasEligibleApproverAsync(db, change, cancellationToken);
        return new ChangeDetail(
            change.Id, change.Number, change.Title, change.Justification, change.AffectedService, change.Risk, change.Impact,
            change.WindowStart, change.WindowEnd, change.ImplementationPlan, change.VerificationPlan, change.BackoutPlan,
            change.OwnerTeamId, row.OwnerTeamName, change.OwnerId, row.OwnerName, change.Status, change.Outcome,
            change.ActualStart, change.ActualEnd, change.BackoutStartedAt, change.VerificationNote, change.BackoutNote,
            change.ClosingNote, change.CancelReason, change.CreatedById, row.CreatedByName, change.CreatedAt, change.UpdatedAt,
            change.ClosedAt, assets, groups, await LoadApprovalAsync(db, change, viewer, cancellationToken), hasEligibleApprover,
            viewer.CanWork(change), change.Version);
    }

    private static async Task<ChangeApprovalInfo?> LoadApprovalAsync(
        IAppDbContext db, ChangeRequest change, ChangeViewer viewer, CancellationToken cancellationToken)
    {
        var approval = await db.ChangeApprovals.AsNoTracking().Where(a => a.ChangeId == change.Id)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Select(a => new
            {
                a.Status,
                a.DecidedById,
                DecidedByName = db.Users.Where(u => u.Id == a.DecidedById).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
                a.Reason,
                a.CreatedAt,
                a.DecidedAt,
                a.Version,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (approval is null)
        {
            return null;
        }

        var canDecide = approval.Status == ChangeApprovalStatus.Pending
            && await ChangeApprovalFlow.CanDecideAsync(db, change.Id, viewer.UserId, cancellationToken);
        return new ChangeApprovalInfo(
            approval.Status, approval.DecidedById, approval.DecidedByName, approval.Reason, approval.CreatedAt, approval.DecidedAt, canDecide, approval.Version);
    }
}
