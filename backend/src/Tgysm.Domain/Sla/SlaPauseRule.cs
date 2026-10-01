using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Sla;

/// <summary>Which timers a hold reason pauses (spec §5.1). A hold reason without a rule pauses nothing.</summary>
public sealed class SlaPauseRule : Entity
{
    private SlaPauseRule()
    {
    }

    public Guid PolicyVersionId { get; private init; }

    public HoldReason HoldReason { get; private init; }

    public bool PausesFirstResponse { get; private init; }

    public bool PausesResolution { get; private init; }

    /// <summary>A rule not yet attached to a version; <see cref="SlaPolicyVersion.Create"/> copies it under the version's id.</summary>
    public static SlaPauseRule Draft(HoldReason holdReason, bool pausesFirstResponse, bool pausesResolution) =>
        Create(Guid.Empty, holdReason, pausesFirstResponse, pausesResolution);

    public static SlaPauseRule Create(Guid policyVersionId, HoldReason holdReason, bool pausesFirstResponse, bool pausesResolution) =>
        new()
        {
            PolicyVersionId = policyVersionId,
            HoldReason = holdReason,
            PausesFirstResponse = pausesFirstResponse,
            PausesResolution = pausesResolution,
        };
}
