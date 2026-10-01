using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Sla;

/// <summary>
/// One timer target of a policy version (spec §5.1). <see cref="RequestTypeId"/> null is the base target for the
/// kind and priority; set, it overrides that base for one request type. Immutable: a change is a new policy version.
/// </summary>
public sealed class SlaTarget : Entity
{
    public const int MinWarningPercent = 1;
    public const int MaxWarningPercent = 99;
    public const int MaxTargetMinutes = 366 * 24 * 60;

    private SlaTarget()
    {
    }

    public Guid PolicyVersionId { get; private init; }

    public TicketKind Kind { get; private init; }

    public Priority Priority { get; private init; }

    public Guid? RequestTypeId { get; private init; }

    public SlaMetric Metric { get; private init; }

    /// <summary>Business minutes allowed.</summary>
    public int TargetMinutes { get; private init; }

    public int WarningPercent { get; private init; }

    /// <summary>A target not yet attached to a version; <see cref="SlaPolicyVersion.Create"/> copies it under the version's id.</summary>
    public static SlaTarget Draft(TicketKind kind, Priority priority, Guid? requestTypeId, SlaMetric metric, int targetMinutes, int warningPercent) =>
        Create(Guid.Empty, kind, priority, requestTypeId, metric, targetMinutes, warningPercent);

    public static SlaTarget Create(
        Guid policyVersionId,
        TicketKind kind,
        Priority priority,
        Guid? requestTypeId,
        SlaMetric metric,
        int targetMinutes,
        int warningPercent)
    {
        if (targetMinutes is <= 0 or > MaxTargetMinutes)
        {
            throw new DomainException(ErrorCodes.InvalidValue, "A target must allow between one minute and one year.");
        }

        if (warningPercent is < MinWarningPercent or > MaxWarningPercent)
        {
            throw new DomainException(ErrorCodes.InvalidValue, "The warning percentage must be between 1 and 99.");
        }

        return new SlaTarget
        {
            PolicyVersionId = policyVersionId,
            Kind = kind,
            Priority = priority,
            RequestTypeId = requestTypeId,
            Metric = metric,
            TargetMinutes = targetMinutes,
            WarningPercent = warningPercent,
        };
    }
}
