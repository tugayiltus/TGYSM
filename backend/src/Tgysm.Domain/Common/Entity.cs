namespace Tgysm.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    /// <summary>
    /// Optimistic concurrency token (ADR-0002). Mapped to PostgreSQL <c>xmin</c> by default; an entity whose row
    /// also carries system bookkeeping can manage it in code instead via <see cref="BumpVersion"/>.
    /// </summary>
    public uint Version { get; private set; }

    /// <summary>Marks a change that concurrent editors must see; only for entities with an application-managed version.</summary>
    protected void BumpVersion() => Version++;
}
