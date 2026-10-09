using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Assets.Import;

public sealed record CommitAssetImportCommand(Guid JobId);

/// <summary>
/// Inserts every row of a previewed job in one transaction, with one movement per asset and one audit event.
/// Committing a committed job returns its result and inserts nothing (serialized per commit).
/// </summary>
public sealed class CommitAssetImportHandler(IAppDbContext db, ICurrentActor actor, IAuditLog audit, TimeProvider clock)
{
    private const string ReferenceInactiveMessage = "A location or department used by the import is no longer active.";

    public async Task<AssetImportJobItem> HandleAsync(CommitAssetImportCommand command, CancellationToken cancellationToken)
    {
        var actorId = actor.UserId ?? throw new AuthenticationFailedException();
        AssetImportJob? job = null;
        await db.RunSerializedAsync(SerializationLocks.AssetImport, async ct =>
        {
            job = await db.AssetImportJobs.SingleOrDefaultAsync(j => j.Id == command.JobId, ct)
                ?? throw new NotFoundException(AuditEntityTypes.AssetImport);
            var now = clock.GetUtcNow();
            if (!job.Commit(now))
            {
                return;
            }

            var rows = JsonSerializer.Deserialize<List<AssetImportRow>>(job.RowsJson, AssetImportJson.Options) ?? [];
            await EnsureStillValidAsync(rows, ct);
            foreach (var row in rows)
            {
                var (asset, movement) = Asset.Create(
                    row.AssetTag, new AssetCatalogEntry(row.ModelId, row.Type, row.Brand, row.Model), row.SerialNumber, row.LocationId, row.DepartmentId,
                    row.WarrantyEndsOn, row.PurchasedOn, row.Notes, now, row.Status, actorId);
                db.Assets.Add(asset);
                db.AssetMovements.Add(movement);
            }

            audit.Record(AuditActions.AssetsImported, AuditEntityTypes.AssetImport, job.Id, newValues: new { job.FileName, Count = rows.Count });
            await db.SaveTranslatingDuplicateAsync(ErrorCodes.AssetTagTaken, AssetRules.TagTakenMessage, ct);
        }, cancellationToken);

        return AssetImportJson.ToItem(job!);
    }

    /// <summary>The inventory may have changed since the preview; any conflict refuses the whole commit.</summary>
    private async Task EnsureStillValidAsync(List<AssetImportRow> rows, CancellationToken cancellationToken)
    {
        var tags = rows.Select(r => Asset.NormalizeTag(r.AssetTag)).ToList();
        if (await db.Assets.AnyAsync(a => tags.Contains(a.NormalizedAssetTag), cancellationToken))
        {
            throw new DomainException(ErrorCodes.AssetTagTaken, AssetRules.TagTakenMessage);
        }

        var serials = rows.Select(r => Asset.NormalizeSerial(r.SerialNumber)).OfType<string>().ToList();
        if (await db.Assets.AnyAsync(a => a.NormalizedSerialNumber != null && serials.Contains(a.NormalizedSerialNumber), cancellationToken))
        {
            throw new DomainException(ErrorCodes.AssetSerialTaken, AssetRules.SerialTakenMessage);
        }

        var locationIds = rows.Select(r => r.LocationId).Distinct().ToList();
        var departmentIds = rows.Select(r => r.DepartmentId).OfType<Guid>().Distinct().ToList();
        var activeLocations = await db.Locations.CountAsync(l => locationIds.Contains(l.Id) && l.IsActive, cancellationToken);
        var activeDepartments = await db.Departments.CountAsync(d => departmentIds.Contains(d.Id) && d.IsActive, cancellationToken);
        if (activeLocations != locationIds.Count || activeDepartments != departmentIds.Count)
        {
            throw new DomainException(ErrorCodes.ReferenceInactive, ReferenceInactiveMessage);
        }
    }
}
