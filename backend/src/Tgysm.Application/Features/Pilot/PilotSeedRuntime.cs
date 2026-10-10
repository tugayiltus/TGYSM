using Tgysm.Application.Abstractions;

namespace Tgysm.Application.Features.Pilot;

/// <summary>
/// The actor of the pilot seed's own service provider: the seed sets the user each handler call acts as (an employee
/// opens a ticket, IT staff works it), the same role <c>HttpCurrentActor</c> plays for a signed-in request. Never
/// registered in the API or the Worker.
/// </summary>
public sealed class PilotSeedActor : ICurrentActor
{
    private const string CorrelationIdPrefix = "pilot-seed-";

    public Guid? UserId { get; set; }

    public string CorrelationId { get; } = CorrelationIdPrefix + Guid.NewGuid().ToString("N");
}

/// <summary>
/// The clock of the pilot seed's own service provider: the seed moves it to the moment each handler call happens, so
/// tickets, SLA instances, timeline events and audit rows spread over the past days as if written then.
/// </summary>
public sealed class PilotSeedClock : TimeProvider
{
    private DateTimeOffset? _now;

    /// <summary>The wall clock, for the seed's own window computation.</summary>
    public static DateTimeOffset RealUtcNow => System.GetUtcNow();

    public void Set(DateTimeOffset now) => _now = now;

    public override DateTimeOffset GetUtcNow() => _now ?? RealUtcNow;
}
