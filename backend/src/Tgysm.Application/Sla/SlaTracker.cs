using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Sla;

/// <summary>
/// Applies ticket actions to the ticket's SLA instances (ADR-0018, spec §5.3). It never saves: the calling handler's
/// single <c>SaveChanges</c> writes the instance changes together with the ticket, history, audit and outbox rows.
/// Running instances use the targets and pause rules of the policy version they started on; only new instances use
/// the current version.
/// </summary>
public sealed class SlaTracker(IAppDbContext db, CalendarProvider calendar, PolicyProvider policies, TimeProvider clock)
{
    public async Task OnCreatedAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        var rules = await calendar.GetRulesAsync(cancellationToken);
        var policy = await policies.GetCurrentAsync(cancellationToken);
        foreach (var metric in Enum.GetValues<SlaMetric>())
        {
            var target = policy.TargetFor(ticket.Kind, ticket.Priority, ticket.RequestTypeId, metric);
            var paused = ticket.Status == TicketStatus.OnHold && ticket.HoldReason is { } reason && PausesMetric(policy, reason, metric);
            db.SlaInstances.Add(SlaInstance.Start(ticket.Id, metric, target, policy.VersionId, ticket.CreatedAt, rules, paused, paused ? ticket.HoldReason : null));
        }
    }

    /// <summary>A hold started or changed: each instance pauses or resumes according to its own version's rule for the reason.</summary>
    public async Task OnHoldAsync(Ticket ticket, HoldReason reason, CancellationToken cancellationToken)
    {
        var (instances, rules, now) = await LoadAsync(ticket, cancellationToken);
        var snapshots = new SnapshotCache(policies);
        foreach (var instance in instances)
        {
            var policy = await snapshots.GetAsync(instance.PolicyVersionId, cancellationToken);
            var shouldPause = PausesMetric(policy, reason, instance.Metric);
            if (shouldPause && !instance.IsPaused)
            {
                instance.Pause(reason, now, rules);
            }
            else if (!shouldPause && instance.IsPaused)
            {
                instance.Resume(now, rules);
            }
        }
    }

    /// <summary>The hold ended (resume or approval granted): every paused instance runs again.</summary>
    public async Task OnResumeAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        var (instances, rules, now) = await LoadAsync(ticket, cancellationToken);
        foreach (var instance in instances.Where(i => i.IsPaused))
        {
            instance.Resume(now, rules);
        }
    }

    /// <summary>The priority changed: each instance gets the new priority's target of the policy version it started on.</summary>
    public async Task OnPriorityChangedAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        var (instances, rules, now) = await LoadAsync(ticket, cancellationToken);
        var snapshots = new SnapshotCache(policies);
        foreach (var instance in instances)
        {
            var policy = await snapshots.GetAsync(instance.PolicyVersionId, cancellationToken);
            var target = policy.TargetFor(ticket.Kind, ticket.Priority, ticket.RequestTypeId, instance.Metric);
            if (target.TargetMinutes != instance.TargetMinutes || target.WarningPercent != instance.WarningPercent)
            {
                instance.ChangeTarget(target.TargetMinutes, target.WarningPercent, now, rules);
            }
        }
    }

    /// <summary>Only a public reply by IT staff or an admin other than the requester answers the requester; it stops first response.</summary>
    public async Task OnPublicReplyAsync(Ticket ticket, Guid authorId, UserRole authorRole, CancellationToken cancellationToken)
    {
        if (authorRole == UserRole.Employee || authorId == ticket.RequesterId)
        {
            return;
        }

        var (instances, rules, now) = await LoadAsync(ticket, cancellationToken);
        instances.SingleOrDefault(i => i.Metric == SlaMetric.FirstResponse)?.Stop(null, now, rules);
    }

    public async Task OnCompletedAsync(Ticket ticket, CancellationToken cancellationToken) =>
        await StopAllAsync(ticket, result: null, cancellationToken);

    public async Task OnCancelledAsync(Ticket ticket, CancellationToken cancellationToken) =>
        await StopAllAsync(ticket, SlaResult.Cancelled, cancellationToken);

    private async Task StopAllAsync(Ticket ticket, SlaResult? result, CancellationToken cancellationToken)
    {
        var (instances, rules, now) = await LoadAsync(ticket, cancellationToken);
        foreach (var instance in instances)
        {
            instance.Stop(result, now, rules);
        }
    }

    private async Task<(List<SlaInstance> Instances, BusinessCalendarRules Rules, DateTimeOffset Now)> LoadAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        var instances = await db.SlaInstances
            .Include(i => i.Pauses.Where(p => p.EndedAt == null))
            .Where(i => i.TicketId == ticket.Id && i.StoppedAt == null)
            .ToListAsync(cancellationToken);
        return (instances, await calendar.GetRulesAsync(cancellationToken), clock.GetUtcNow());
    }

    private static bool PausesMetric(SlaPolicySnapshot policy, HoldReason reason, SlaMetric metric)
    {
        var rule = policy.PauseRules.FirstOrDefault(r => r.HoldReason == reason);
        return rule is not null && (metric == SlaMetric.FirstResponse ? rule.PausesFirstResponse : rule.PausesResolution);
    }

    private sealed class SnapshotCache(PolicyProvider policies)
    {
        private readonly Dictionary<Guid, SlaPolicySnapshot> _snapshots = [];

        public async Task<SlaPolicySnapshot> GetAsync(Guid versionId, CancellationToken cancellationToken)
        {
            if (!_snapshots.TryGetValue(versionId, out var snapshot))
            {
                snapshot = await policies.GetVersionAsync(versionId, cancellationToken);
                _snapshots[versionId] = snapshot;
            }

            return snapshot;
        }
    }
}
