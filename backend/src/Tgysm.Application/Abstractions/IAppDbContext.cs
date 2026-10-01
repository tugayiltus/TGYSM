using Microsoft.EntityFrameworkCore;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Catalog;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Operations;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Sla;
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
    DbSet<Ticket> Tickets { get; }
    DbSet<Approval> Approvals { get; }
    DbSet<TicketAssignment> TicketAssignments { get; }
    DbSet<TicketComment> TicketComments { get; }
    DbSet<TicketAttachment> TicketAttachments { get; }
    DbSet<TicketEvent> TicketEvents { get; }
    DbSet<TicketRelation> TicketRelations { get; }
    DbSet<TicketAsset> TicketAssets { get; }
    DbSet<Category> Categories { get; }
    DbSet<RequestType> RequestTypes { get; }
    DbSet<WorkerHeartbeat> WorkerHeartbeats { get; }
    DbSet<OutboxEvent> OutboxEvents { get; }
    DbSet<EmailTemplate> EmailTemplates { get; }
    DbSet<EmailDelivery> EmailDeliveries { get; }
    DbSet<PasswordToken> PasswordTokens { get; }
    DbSet<BusinessCalendar> BusinessCalendars { get; }
    DbSet<BusinessHours> BusinessHours { get; }
    DbSet<Holiday> Holidays { get; }
    DbSet<SlaPolicyVersion> SlaPolicyVersions { get; }
    DbSet<SlaTarget> SlaTargets { get; }
    DbSet<SlaPauseRule> SlaPauseRules { get; }
    DbSet<SlaInstance> SlaInstances { get; }
    DbSet<SlaPause> SlaPauses { get; }
    DbSet<SlaEvent> SlaEvents { get; }

    /// <summary>
    /// Saves all tracked changes in one transaction. Throws <see cref="Common.ConcurrencyException"/> on a stale
    /// <see cref="Entity.Version"/> and <see cref="DomainException"/> with <see cref="ErrorCodes.Duplicate"/> on a unique violation.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Makes the next save fail with a concurrency error unless the row still has <paramref name="expectedVersion"/>.</summary>
    void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : Entity;

    /// <summary>Runs <paramref name="work"/> in one transaction (several saves commit together).</summary>
    Task RunInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="work"/> in one transaction that first takes the advisory lock <paramref name="lockKey"/>,
    /// so check-then-write rules that span rows (last admin, location cycles) cannot race.
    /// </summary>
    Task RunSerializedAsync(long lockKey, Func<CancellationToken, Task> work, CancellationToken cancellationToken);
}
