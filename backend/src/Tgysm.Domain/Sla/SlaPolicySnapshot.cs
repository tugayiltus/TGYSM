using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Sla;

/// <summary>A read-only copy of one policy version, what SLA instances are started from (ADR-0018).</summary>
public sealed record SlaPolicySnapshot(Guid VersionId, IReadOnlyList<SlaTarget> Targets, IReadOnlyList<SlaPauseRule> PauseRules)
{
    /// <summary>The target for a ticket: the request type's override when there is one, else the base target for the kind and priority.</summary>
    public SlaTarget TargetFor(TicketKind kind, Priority priority, Guid? requestTypeId, SlaMetric metric)
    {
        SlaTarget? baseTarget = null;
        foreach (var target in Targets)
        {
            if (target.Kind != kind || target.Priority != priority || target.Metric != metric)
            {
                continue;
            }

            if (requestTypeId is not null && target.RequestTypeId == requestTypeId)
            {
                return target;
            }

            if (target.RequestTypeId is null)
            {
                baseTarget = target;
            }
        }

        return baseTarget ?? throw new DomainException(ErrorCodes.SlaTargetMissing, "The policy has no target for this ticket.");
    }
}
