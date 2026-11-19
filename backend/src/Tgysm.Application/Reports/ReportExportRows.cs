using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Reports;

namespace Tgysm.Application.Reports;

/// <summary>Column keys of the export sheets; <c>ExportHeaders</c> holds one Turkish header per key.</summary>
public static class ExportColumns
{
    public const string Number = "number";
    public const string Kind = "kind";
    public const string Subject = "subject";
    public const string Status = "status";
    public const string Priority = "priority";
    public const string Team = "team";
    public const string Assignee = "assignee";
    public const string CreatedAt = "created_at";
    public const string CompletedAt = "completed_at";
    public const string AssetTag = "asset_tag";
    public const string Type = "type";
    public const string Brand = "brand";
    public const string Model = "model";
    public const string Location = "location";
    public const string WarrantyEndsOn = "warranty_ends_on";
    public const string Title = "title";
    public const string Outcome = "outcome";
    public const string Risk = "risk";
    public const string WindowStart = "window_start";
    public const string ActualEnd = "actual_end";

    public static readonly IReadOnlyList<string> Tickets =
        [Number, Kind, Subject, Status, Priority, Team, Assignee, CreatedAt, CompletedAt];

    public static readonly IReadOnlyList<string> Assets =
        [AssetTag, Type, Brand, Model, Status, Location, WarrantyEndsOn];

    public static readonly IReadOnlyList<string> Changes =
        [Number, Title, Status, Outcome, Risk, Team, WindowStart, ActualEnd];
}

/// <summary>Turns the ordered drill-down rows into export cells, so an export lists exactly the rows the drill-down pages through.</summary>
public static class ReportExportRows
{
    public static IReadOnlyList<string> ColumnKeysOf(DrilldownKind kind) => kind switch
    {
        DrilldownKind.Assets => ExportColumns.Assets,
        DrilldownKind.Changes => ExportColumns.Changes,
        _ => ExportColumns.Tickets,
    };

    /// <summary>The cell content for <paramref name="rows"/>; the rows are read lazily while the file is written.</summary>
    public static ReportExportContent ContentOf(
        DrilldownRows rows,
        ReportName report,
        string metric,
        string? bucket,
        ReportDrillPart part,
        ReportFilter filter,
        string scopeLabel,
        IReadOnlyDictionary<string, string> names) =>
        new(report, metric, bucket, part, filter, scopeLabel, ColumnKeysOf(rows.Kind), Cells(rows), names);

    public static Task<int> CountAsync(DrilldownRows rows, CancellationToken cancellationToken) => rows.Kind switch
    {
        DrilldownKind.Assets => rows.Assets!.CountAsync(cancellationToken),
        DrilldownKind.Changes => rows.Changes!.CountAsync(cancellationToken),
        _ => rows.Tickets!.CountAsync(cancellationToken),
    };

    private static async IAsyncEnumerable<IReadOnlyList<object?>> Cells(DrilldownRows rows)
    {
        if (rows.Kind == DrilldownKind.Assets)
        {
            await foreach (var a in rows.Assets!.AsAsyncEnumerable())
            {
                yield return [a.AssetTag, a.Type, a.Brand, a.Model, a.Status, a.LocationName, a.WarrantyEndsOn];
            }

            yield break;
        }

        if (rows.Kind == DrilldownKind.Changes)
        {
            await foreach (var c in rows.Changes!.AsAsyncEnumerable())
            {
                yield return [c.Number, c.Title, c.Status, c.Outcome, c.Risk, c.OwnerTeamName, c.WindowStart, c.ActualEnd];
            }

            yield break;
        }

        await foreach (var t in rows.Tickets!.AsAsyncEnumerable())
        {
            yield return [t.Number, t.Kind, t.Subject, t.Status, t.Priority, t.TeamName, t.AssigneeName, t.CreatedAt, t.CompletedAt];
        }
    }
}
