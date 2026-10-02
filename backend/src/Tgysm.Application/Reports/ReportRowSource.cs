using FluentValidation;
using FluentValidation.Results;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports;

/// <summary>Which half of a ratio a drill-down lists. Only ratios have a <see cref="Denominator"/>.</summary>
public enum ReportDrillPart
{
    Numerator,
    Denominator,
}

public enum DrilldownKind
{
    Tickets,
    Assets,
}

public sealed record DrilldownTicketRow(
    string Number, TicketKind Kind, string Subject, TicketStatus Status, Priority Priority, string TeamName, string? AssigneeName,
    DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);

public sealed record DrilldownAssetRow(
    Guid Id, string AssetTag, AssetType Type, string Brand, string Model, AssetStatus Status, string LocationName, DateOnly? WarrantyEndsOn);

/// <summary>The ordered rows behind one figure: exactly one of <see cref="Tickets"/> / <see cref="Assets"/> is set, matching <see cref="Kind"/>.</summary>
public sealed record DrilldownRows(DrilldownKind Kind, IQueryable<DrilldownTicketRow>? Tickets, IQueryable<DrilldownAssetRow>? Assets);

/// <summary>
/// The rows behind a report figure, ordered so any page of them is stable. The drill-down endpoint pages this query; the Excel
/// export reads the same one, so a figure, its drill-down and its export always list the same rows.
/// </summary>
public sealed class ReportRowSource(ReportScope scope, TicketReportSource source, IAppDbContext db, TimeProvider clock, ICompanyClock companyClock)
{
    private const string MetricField = "metric";
    private const string PartField = "part";
    private const string BucketField = "bucket";
    private const string NameSeparator = " ";

    /// <summary>
    /// The rows of <paramref name="metricKey"/> of <paramref name="report"/> for <paramref name="viewer"/>: the metric's numerator rows,
    /// a ratio's denominator rows, or the rows of one distribution <paramref name="bucket"/>. An unknown metric, a denominator on a
    /// non-ratio, or an unknown or unsupported bucket is a 400 field error.
    /// </summary>
    public DrilldownRows Open(ReportViewer viewer, ReportName report, string metricKey, string? bucket, ReportDrillPart part, ReportFilter filter)
    {
        filter.EnsureValid();
        var metric = ReportCatalog.Find(report, metricKey);
        var now = clock.GetUtcNow();
        if (metric is AssetReportMetric assetMetric)
        {
            return OpenAssets(viewer, assetMetric, bucket, part, filter, now);
        }

        var effective = ReportCatalog.EffectiveFilter(report, filter);
        var ticketMetric = (ReportMetric)metric;
        var filtered = source.Apply(scope.TicketsFor(viewer), effective, ticketMetric.DateBasis);
        var context = new ReportContext(db, now, source.Window(effective));
        var rows = TicketRows(ticketMetric, filtered, context, bucket, part);
        return new DrilldownRows(DrilldownKind.Tickets, ProjectTickets(rows), null);
    }

    private static IQueryable<Ticket> TicketRows(ReportMetric metric, IQueryable<Ticket> filtered, ReportContext context, string? bucket, ReportDrillPart part)
    {
        if (part == ReportDrillPart.Denominator)
        {
            if (bucket is not null)
            {
                throw Invalid(BucketField);
            }

            return metric.DenominatorRows(filtered, context) ?? throw Invalid(PartField);
        }

        if (bucket is null)
        {
            return metric.Rows(filtered, context);
        }

        try
        {
            return metric.BucketRows(filtered, context, bucket);
        }
        catch (NotSupportedException)
        {
            throw Invalid(BucketField);
        }
    }

    // Order by creation then id; transfer rows repeat a ticket once per history row, and those repeats are identical, so any page split is the same.
    private IQueryable<DrilldownTicketRow> ProjectTickets(IQueryable<Ticket> rows) =>
        rows.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .Select(t => new DrilldownTicketRow(
                t.Number, t.Kind, t.Subject, t.Status, t.Priority,
                db.Teams.Where(x => x.Id == t.TeamId).Select(x => x.Name).First(),
                db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
                t.CreatedAt, t.CompletedAt));

    private DrilldownRows OpenAssets(ReportViewer viewer, AssetReportMetric metric, string? bucket, ReportDrillPart part, ReportFilter filter, DateTimeOffset now)
    {
        if (part == ReportDrillPart.Denominator)
        {
            throw Invalid(PartField);
        }

        var assets = AssetReportSource.Apply(scope.AssetsFor(viewer), filter);
        var context = AssetReportContext.Create(db, filter, now, companyClock.Zone);
        IQueryable<Asset> rows;
        try
        {
            rows = bucket is null ? metric.Rows(assets, context) : metric.BucketRows(assets, context, bucket);
        }
        catch (NotSupportedException)
        {
            throw Invalid(BucketField);
        }

        return new DrilldownRows(DrilldownKind.Assets, null, rows.OrderBy(a => a.AssetTag).ThenBy(a => a.Id)
            .Select(a => new DrilldownAssetRow(
                a.Id, a.AssetTag, a.Type, a.Brand, a.Model, a.Status,
                db.Locations.Where(l => l.Id == a.LocationId).Select(l => l.Name).First(), a.WarrantyEndsOn)));
    }

    private static ValidationException Invalid(string field) =>
        new([new ValidationFailure(field, "Not available for this metric.") { ErrorCode = ErrorCodes.ReportsInvalidFilter }]);
}
