using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Organization;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Assets.Lifecycle;

/// <summary>
/// Every lifecycle operation: load the asset and its active assignment, apply the domain transition, write the
/// movement and audit, all in one serialized transaction (ADR-0010).
/// </summary>
public sealed class AssetLifecycleHandler(
    IAppDbContext db,
    IServiceProvider validators,
    OrganizationReferences references,
    ICurrentActor actor,
    IAuditLog audit,
    TimeProvider clock)
{
    private delegate Task<AssetChange> Operation(Asset asset, AssetAssignment? active, Guid actorUserId, DateTimeOffset now, CancellationToken cancellationToken);

    public async Task<AssetItem> AssignAsync(AssignAssetCommand command, CancellationToken cancellationToken)
    {
        await ValidateAsync(command, cancellationToken);
        return await RunAsync(command.AssetId, command.Version, AuditActions.AssetAssigned, async (asset, _, actorId, now, ct) =>
        {
            await EnsureActiveUserAsync(command.UserId, ct);
            if (command.RequestTicketId is { } requestId)
            {
                await Tickets.ApprovalRules.EnsureHandoverAllowedAsync(db, requestId, ct);
            }

            return asset.Assign(command.UserId, actorId, command.RequestTicketId, command.Note, now);
        }, cancellationToken);
    }

    public async Task<AssetItem> ReturnAsync(ReturnAssetCommand command, CancellationToken cancellationToken)
    {
        await ValidateAsync(command, cancellationToken);
        return await RunAsync(command.AssetId, command.Version, AuditActions.AssetReturned, async (asset, active, actorId, now, ct) =>
        {
            await references.EnsureActiveAsync(null, command.LocationId, ct);
            return asset.Return(RequireActive(active), actorId, command.LocationId, now);
        }, cancellationToken);
    }

    public async Task<AssetItem> TransferAsync(TransferAssetCommand command, CancellationToken cancellationToken)
    {
        await ValidateAsync(command, cancellationToken);
        return await RunAsync(command.AssetId, command.Version, AuditActions.AssetTransferred, async (asset, active, actorId, now, ct) =>
        {
            await EnsureActiveUserAsync(command.UserId, ct);
            return asset.Transfer(RequireActive(active), command.UserId, actorId, command.Note, now);
        }, cancellationToken);
    }

    public Task<AssetItem> StartMaintenanceAsync(StartMaintenanceCommand command, CancellationToken cancellationToken) =>
        RunAsync(command.AssetId, command.Version, AuditActions.AssetMaintenanceStarted, (asset, active, actorId, now, _) =>
            Task.FromResult(asset.StartMaintenance(active, command.RetainResponsibility, actorId, now)), cancellationToken);

    public Task<AssetItem> EndMaintenanceAsync(EndMaintenanceCommand command, CancellationToken cancellationToken) =>
        RunAsync(command.AssetId, command.Version, AuditActions.AssetMaintenanceEnded, (asset, active, actorId, now, _) =>
            Task.FromResult(asset.EndMaintenance(active, actorId, now)), cancellationToken);

    public async Task<AssetItem> MarkLostAsync(MarkAssetLostCommand command, CancellationToken cancellationToken)
    {
        await ValidateAsync(command, cancellationToken);
        return await RunAsync(command.AssetId, command.Version, AuditActions.AssetLost, (asset, active, actorId, now, _) =>
            Task.FromResult(asset.MarkLost(active, actorId, command.Reason, now)), cancellationToken);
    }

    public async Task<AssetItem> MarkFoundAsync(MarkAssetFoundCommand command, CancellationToken cancellationToken)
    {
        await ValidateAsync(command, cancellationToken);
        return await RunAsync(command.AssetId, command.Version, AuditActions.AssetFound, async (asset, _, actorId, now, ct) =>
        {
            await references.EnsureActiveAsync(null, command.LocationId, ct);
            return asset.MarkFound(actorId, command.LocationId, now);
        }, cancellationToken);
    }

    public async Task<AssetItem> RetireAsync(RetireAssetCommand command, CancellationToken cancellationToken)
    {
        await ValidateAsync(command, cancellationToken);
        return await RunAsync(command.AssetId, command.Version, AuditActions.AssetRetired, (asset, active, actorId, now, _) =>
            Task.FromResult(asset.Retire(active, actorId, command.Reason, now)), cancellationToken);
    }

    private async Task<AssetItem> RunAsync(Guid assetId, uint version, string auditAction, Operation operation, CancellationToken cancellationToken)
    {
        var actorId = actor.UserId ?? throw new AuthenticationFailedException();
        await db.RunSerializedAsync(SerializationLocks.AssetLifecycle, async ct =>
        {
            var asset = await db.Assets.SingleOrDefaultAsync(a => a.Id == assetId, ct)
                ?? throw new NotFoundException(AuditEntityTypes.Asset);
            db.SetExpectedVersion(asset, version);
            var active = await db.AssetAssignments.SingleOrDefaultAsync(a => a.AssetId == assetId && a.ReturnedAt == null, ct);

            var change = await operation(asset, active, actorId, clock.GetUtcNow(), ct);
            db.AssetMovements.Add(change.Movement);
            audit.Record(auditAction, AuditEntityTypes.Asset, asset.Id,
                new { Status = change.Movement.FromStatus },
                new { Status = change.Movement.ToStatus, change.Movement.UserId, change.Movement.Reason });

            // Close the old assignment before opening a new one: the partial unique index allows one active row.
            await db.SaveChangesAsync(ct);
            if (change.OpenedAssignment is { } opened)
            {
                db.AssetAssignments.Add(opened);
                await db.SaveChangesAsync(ct);
            }
        }, cancellationToken);

        return await AssetQueries.LoadItemAsync(db, assetId, cancellationToken);
    }

    private async Task EnsureActiveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.IsActive, cancellationToken))
        {
            throw new DomainException(ErrorCodes.AssetUserInactive, "Assets can only be handed to active users.");
        }
    }

    private static AssetAssignment RequireActive(AssetAssignment? active) =>
        active ?? throw new DomainException(ErrorCodes.AssetInvalidTransition, "The asset has no active assignment.");

    private Task ValidateAsync<TCommand>(TCommand command, CancellationToken cancellationToken) =>
        ((IValidator<TCommand>)validators.GetService(typeof(IValidator<TCommand>))!).ValidateAndThrowAsync(command, cancellationToken);
}
