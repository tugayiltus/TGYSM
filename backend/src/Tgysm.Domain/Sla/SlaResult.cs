namespace Tgysm.Domain.Sla;

/// <summary>How a stopped SLA instance ended (spec §5.1).</summary>
public enum SlaResult
{
    Met,
    Breached,
    Cancelled,
}
