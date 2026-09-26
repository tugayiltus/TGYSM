using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Tickets;

public sealed record CreateRelatedRequestCommand(string Number, Guid RequestTypeId);

public sealed record LinkTicketAssetCommand(string Number, Guid AssetId);

public sealed record UnlinkTicketAssetCommand(string Number, Guid AssetId);

public sealed record ListTicketAssetsQuery(string Number);

public sealed record ListAssetTicketsQuery(Guid AssetId);

public sealed record TicketAssetItem(Guid AssetId, string AssetTag, Domain.Assets.AssetType Type, string Brand, string Model, DateTimeOffset LinkedAt);

internal sealed class CreateRelatedRequestValidator : AbstractValidator<CreateRelatedRequestCommand>
{
    public CreateRelatedRequestValidator() => RuleFor(c => c.RequestTypeId).NotEmpty();
}

/// <summary>
/// IT opens a request from an incident (spec §3.8): subject, description, requester, location and linked assets are
/// copied; comments and files are not. Both tickets are linked and the request starts its own approval.
/// </summary>
public sealed class CreateRelatedRequestHandler(
    IAppDbContext db, IValidator<CreateRelatedRequestCommand> validator, TicketScope scope, TicketFactory factory, IAuditLog audit)
{
    public async Task<TicketDetail> HandleAsync(CreateRelatedRequestCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var (incident, viewer) = await scope.LoadForWorkAsync(command.Number, cancellationToken);
        if (incident.Kind != TicketKind.Incident || incident.IsClosed)
        {
            throw new DomainException(ErrorCodes.TicketInvalidTransition, "Related requests are created from open incidents.");
        }

        var type = await db.RequestTypes.AsNoTracking().SingleOrDefaultAsync(r => r.Id == command.RequestTypeId && r.IsActive, cancellationToken)
            ?? throw new DomainException(ErrorCodes.ReferenceInactive, "The request type is inactive or missing.");
        var assetIds = await db.TicketAssets.Where(l => l.TicketId == incident.Id).Select(l => l.AssetId).ToListAsync(cancellationToken);

        return await factory.CreateAsync(TicketKind.Request, null, incident.LocationId, async (number, requester, team, now) =>
        {
            var request = Ticket.CreateRequest(number, incident.Subject, incident.Description, null, incident.LocationId, requester, team, type.Id, type.RequiresApproval, now);
            if (type.RequiresApproval)
            {
                db.Approvals.Add(Approval.Request(request.Id, await ApprovalRules.ApproverForAsync(db, requester, cancellationToken), now));
            }

            db.TicketRelations.Add(TicketRelation.Create(incident.Id, request.Id, now));
            db.TicketAssets.AddRange(assetIds.Select(id => TicketAsset.Create(request.Id, id, now)));
            incident.Touch(now);
            TicketTimeline.Record(db, incident, TicketEventKind.RelatedRequestCreated, Visibility.Public, viewer.UserId, now, new { requestNumber = number });
            audit.Record(AuditActions.RelatedRequestCreated, AuditEntityTypes.Ticket, incident.Id, newValues: new { requestNumber = number });
            return request;
        }, cancellationToken, requesterId: incident.RequesterId);
    }
}

internal static class TicketAssetQueries
{
    public static async Task<IReadOnlyList<TicketAssetItem>> ListAsync(IAppDbContext db, Guid ticketId, CancellationToken cancellationToken) =>
        await (
            from l in db.TicketAssets.AsNoTracking()
            join a in db.Assets on l.AssetId equals a.Id
            where l.TicketId == ticketId
            orderby l.CreatedAt
            select new TicketAssetItem(a.Id, a.AssetTag, a.Type, a.Brand, a.Model, l.CreatedAt)).ToListAsync(cancellationToken);
}

/// <summary>People working on the ticket link any asset; the requester links only an asset assigned to them.</summary>
public sealed class LinkTicketAssetHandler(IAppDbContext db, TicketScope scope, IAuditLog audit, TimeProvider clock)
{
    public async Task<IReadOnlyList<TicketAssetItem>> HandleAsync(LinkTicketAssetCommand command, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = await scope.LoadAsync(command.Number, cancellationToken);
        var allowed = viewer.CanWork(ticket)
            ? await db.Assets.AnyAsync(a => a.Id == command.AssetId, cancellationToken)
            : await db.AssetAssignments.AnyAsync(a => a.AssetId == command.AssetId && a.UserId == viewer.UserId && a.ReturnedAt == null, cancellationToken);
        if (!allowed)
        {
            throw new NotFoundException(AuditEntityTypes.Asset);
        }

        if (await db.TicketAssets.AnyAsync(l => l.TicketId == ticket.Id && l.AssetId == command.AssetId, cancellationToken))
        {
            throw new DomainException(ErrorCodes.Duplicate, "The asset is already linked.");
        }

        var now = clock.GetUtcNow();
        db.TicketAssets.Add(TicketAsset.Create(ticket.Id, command.AssetId, now));
        ticket.Touch(now);
        TicketTimeline.Record(db, ticket, TicketEventKind.AssetLinked, Visibility.Public, viewer.UserId, now, new { command.AssetId });
        audit.Record(AuditActions.TicketAssetLinked, AuditEntityTypes.Ticket, ticket.Id, newValues: new { command.AssetId });
        await db.SaveChangesAsync(cancellationToken);
        return await TicketAssetQueries.ListAsync(db, ticket.Id, cancellationToken);
    }
}

public sealed class UnlinkTicketAssetHandler(IAppDbContext db, TicketScope scope, IAuditLog audit, TimeProvider clock)
{
    public async Task<IReadOnlyList<TicketAssetItem>> HandleAsync(UnlinkTicketAssetCommand command, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = await scope.LoadForWorkAsync(command.Number, cancellationToken);
        var link = await db.TicketAssets.SingleOrDefaultAsync(l => l.TicketId == ticket.Id && l.AssetId == command.AssetId, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Asset);
        var now = clock.GetUtcNow();
        db.TicketAssets.Remove(link);
        ticket.Touch(now);
        TicketTimeline.Record(db, ticket, TicketEventKind.AssetUnlinked, Visibility.Public, viewer.UserId, now, new { command.AssetId });
        audit.Record(AuditActions.TicketAssetUnlinked, AuditEntityTypes.Ticket, ticket.Id, new { command.AssetId });
        await db.SaveChangesAsync(cancellationToken);
        return await TicketAssetQueries.ListAsync(db, ticket.Id, cancellationToken);
    }
}

public sealed class ListTicketAssetsHandler(IAppDbContext db, TicketScope scope)
{
    public async Task<IReadOnlyList<TicketAssetItem>> HandleAsync(ListTicketAssetsQuery query, CancellationToken cancellationToken)
    {
        var (ticket, _) = await scope.LoadAsync(query.Number, cancellationToken);
        return await TicketAssetQueries.ListAsync(db, ticket.Id, cancellationToken);
    }
}

/// <summary>Tickets linked to an asset, limited to the viewer's ticket scope (IT staff and admins only).</summary>
public sealed class ListAssetTicketsHandler(IAppDbContext db, TicketScope scope)
{
    public async Task<IReadOnlyList<RelatedTicketItem>> HandleAsync(ListAssetTicketsQuery query, CancellationToken cancellationToken)
    {
        if (!await db.Assets.AnyAsync(a => a.Id == query.AssetId, cancellationToken))
        {
            throw new NotFoundException(AuditEntityTypes.Asset);
        }

        var viewer = await scope.ViewerAsync(cancellationToken);
        var ids = await db.TicketAssets.AsNoTracking().Where(l => l.AssetId == query.AssetId).Select(l => l.TicketId).ToListAsync(cancellationToken);
        return await TicketQueries.VisibleTicketsAsync(db, ids, viewer, cancellationToken);
    }
}
