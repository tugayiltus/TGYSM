using Tgysm.Application.Abstractions;
using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports;

/// <summary>Related-row queries shared by the date windows and the metrics, so both read the same rows.</summary>
public static class ReportQueries
{
    /// <summary>First-response timers that stopped with a response (a cancelled timer is not a response).</summary>
    public static IQueryable<SlaInstance> FirstResponses(IAppDbContext db) =>
        db.SlaInstances.Where(i => i.Metric == SlaMetric.FirstResponse && i.StoppedAt != null && i.Result != SlaResult.Cancelled);

    /// <summary>
    /// Approvals that were decided. A voided approval is not one, so a ticket has at most one: an approved approval that is voided
    /// is followed by a new request, and a rejection cancels the ticket.
    /// </summary>
    public static IQueryable<Approval> DecidedApprovals(IAppDbContext db) =>
        db.Approvals.Where(a => (a.Status == ApprovalStatus.Approved || a.Status == ApprovalStatus.Rejected) && a.DecidedAt != null);

    public static IQueryable<SlaInstance> ResolutionTimers(IAppDbContext db) =>
        db.SlaInstances.Where(i => i.Metric == SlaMetric.Resolution);
}
