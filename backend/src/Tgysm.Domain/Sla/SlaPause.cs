using Tgysm.Domain.Tickets;

namespace Tgysm.Domain.Sla;

/// <summary>One stretch during which an SLA instance did not run; <see cref="EndedAt"/> is null while it lasts.</summary>
public sealed class SlaPause
{
    private SlaPause()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid SlaInstanceId { get; private init; }

    public HoldReason HoldReason { get; private init; }

    public DateTimeOffset StartedAt { get; private init; }

    public DateTimeOffset? EndedAt { get; private set; }

    public static SlaPause Begin(Guid slaInstanceId, HoldReason holdReason, DateTimeOffset now) =>
        new() { SlaInstanceId = slaInstanceId, HoldReason = holdReason, StartedAt = now };

    public void End(DateTimeOffset now) => EndedAt = now;
}
