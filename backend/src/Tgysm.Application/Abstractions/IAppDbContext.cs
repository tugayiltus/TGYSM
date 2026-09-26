using Microsoft.EntityFrameworkCore;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Department> Departments { get; }
    DbSet<Location> Locations { get; }
    DbSet<Team> Teams { get; }
    DbSet<TeamMembership> TeamMemberships { get; }
    DbSet<AuditEvent> AuditEvents { get; }
    DbSet<Asset> Assets { get; }
    DbSet<AssetAssignment> AssetAssignments { get; }
    DbSet<AssetMovement> AssetMovements { get; }
    DbSet<AssetImportJob> AssetImportJobs { get; }

    /// <summary>
    /// Saves all tracked changes in one transaction. Throws <see cref="Common.ConcurrencyException"/> on a stale
    /// <see cref="Entity.Version"/> and <see cref="DomainException"/> with <see cref="ErrorCodes.Duplicate"/> on a unique violation.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Makes the next save fail with a concurrency error unless the row still has <paramref name="expectedVersion"/>.</summary>
    void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : Entity;

    /// <summary>
    /// Runs <paramref name="work"/> in one transaction that first takes the advisory lock <paramref name="lockKey"/>,
    /// so check-then-write rules that span rows (last admin, location cycles) cannot race.
    /// </summary>
    Task RunSerializedAsync(long lockKey, Func<CancellationToken, Task> work, CancellationToken cancellationToken);
}
