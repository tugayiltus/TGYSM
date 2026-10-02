using System.Globalization;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports;

/// <summary>Which instant of a ticket the day window applies to.</summary>
public enum ReportDateBasis
{
    /// <summary>The ticket's creation.</summary>
    Created,

    /// <summary>The ticket's closing (completion, cancellation or approval rejection).</summary>
    Completed,

    /// <summary>The stop of the first-response timer (cancelled timers do not count).</summary>
    FirstResponded,

    /// <summary>The decision of an approval (approved or rejected).</summary>
    Decided,

    /// <summary>No window: a snapshot of tickets as they are now (open backlog).</summary>
    None,
}

public enum SlaStateFilter
{
    Met,
    Breached,
    OpenBreached,
    AtRisk,
}

/// <summary>Query-string parameter names of <see cref="ReportFilter"/>, shared by the binder and <see cref="ReportFilter.ToQueryString"/>.</summary>
public static class ReportFilterKeys
{
    public const string From = "from";
    public const string To = "to";
    public const string Kind = "kind";
    public const string Priority = "priority";
    public const string CategoryId = "categoryId";
    public const string TeamId = "teamId";
    public const string AssigneeId = "assigneeId";
    public const string DepartmentId = "departmentId";
    public const string LocationId = "locationId";
    public const string Status = "status";
    public const string SlaState = "slaState";
    public const string AssetType = "assetType";
    public const string AssetStatus = "assetStatus";

    /// <summary>Format of <c>from</c> and <c>to</c>.</summary>
    public const string DateFormat = "yyyy-MM-dd";
}

/// <summary>A half-open instant range: <c>FromInclusive &lt;= x &lt; ToExclusive</c>; a null bound is open.</summary>
public readonly record struct ReportWindow(DateTimeOffset? FromInclusive, DateTimeOffset? ToExclusive);

/// <summary>
/// Filter shared by every report (spec §7.1). <c>From</c>/<c>To</c> are whole days in the company time zone, <c>To</c> inclusive.
/// Ticket reports use the ticket fields, asset reports the asset ones; the rest is ignored.
/// </summary>
public sealed record ReportFilter
{
    private const string FromField = "from";
    private const string RangeField = "to";

    /// <summary>Earliest and latest day a window may name; keeps the day arithmetic far from <see cref="DateOnly"/> overflow.</summary>
    public static readonly DateOnly MinDay = new(2000, 1, 1);
    public static readonly DateOnly MaxDay = new(2100, 12, 31);

    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public TicketKind? Kind { get; init; }
    public Priority? Priority { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? TeamId { get; init; }
    public Guid? AssigneeId { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? LocationId { get; init; }
    public TicketStatus? Status { get; init; }
    public SlaStateFilter? SlaState { get; init; }
    public AssetType? AssetType { get; init; }
    public AssetStatus? AssetStatus { get; init; }

    /// <summary>Rejects a day outside <see cref="MinDay"/>..<see cref="MaxDay"/> and <c>From &gt; To</c> as 400 field errors (<see cref="ErrorCodes.ReportsInvalidFilter"/>).</summary>
    public void EnsureValid()
    {
        if (From is { } early && (early < MinDay || early > MaxDay))
        {
            throw new ValidationException([new ValidationFailure(FromField, "Day out of range.") { ErrorCode = ErrorCodes.ReportsInvalidFilter }]);
        }

        if (To is { } late && (late < MinDay || late > MaxDay))
        {
            throw new ValidationException([new ValidationFailure(RangeField, "Day out of range.") { ErrorCode = ErrorCodes.ReportsInvalidFilter }]);
        }

        if (From is { } from && To is { } to && from > to)
        {
            throw new ValidationException([new ValidationFailure(RangeField, "From must not be after to.") { ErrorCode = ErrorCodes.ReportsInvalidFilter }]);
        }
    }

    /// <summary>The instant range of the day window in <paramref name="zone"/>; <c>To</c> covers its whole day.</summary>
    public ReportWindow Window(TimeZoneInfo zone) =>
        new(From is { } from ? StartOfDay(from, zone) : null, To is { } to ? StartOfDay(to.AddDays(1), zone) : null);

    /// <summary>Query string (no leading '?') that <c>ReportFilterBinder.Bind</c> reads back to an equal filter.</summary>
    public string ToQueryString()
    {
        var parts = new List<KeyValuePair<string, string?>>
        {
            new(ReportFilterKeys.From, From?.ToString(ReportFilterKeys.DateFormat, CultureInfo.InvariantCulture)),
            new(ReportFilterKeys.To, To?.ToString(ReportFilterKeys.DateFormat, CultureInfo.InvariantCulture)),
            new(ReportFilterKeys.Kind, EnumName(Kind)),
            new(ReportFilterKeys.Priority, EnumName(Priority)),
            new(ReportFilterKeys.CategoryId, CategoryId?.ToString()),
            new(ReportFilterKeys.TeamId, TeamId?.ToString()),
            new(ReportFilterKeys.AssigneeId, AssigneeId?.ToString()),
            new(ReportFilterKeys.DepartmentId, DepartmentId?.ToString()),
            new(ReportFilterKeys.LocationId, LocationId?.ToString()),
            new(ReportFilterKeys.Status, EnumName(Status)),
            new(ReportFilterKeys.SlaState, EnumName(SlaState)),
            new(ReportFilterKeys.AssetType, EnumName(AssetType)),
            new(ReportFilterKeys.AssetStatus, EnumName(AssetStatus)),
        };
        return string.Join('&', parts.Where(p => p.Value is not null).Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}"));
    }

    /// <summary>The snake_case query name of an enum value ("on_hold"), the form URLs use everywhere.</summary>
    public static string EnumName<TEnum>(TEnum value)
        where TEnum : struct, Enum => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    private static string? EnumName<TEnum>(TEnum? value)
        where TEnum : struct, Enum => value is { } v ? EnumName(v) : null;

    private static DateTimeOffset StartOfDay(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1); // the zone skips local midnight (DST gap): the day starts when the clock resumes
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
    }
}
