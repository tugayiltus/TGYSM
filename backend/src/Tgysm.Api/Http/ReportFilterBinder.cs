using System.Globalization;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Tgysm.Application.Reports;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.Http;

/// <summary>
/// Reads a <see cref="ReportFilter"/> from a query string. Any bad enum, guid or date (and <c>from &gt; to</c>) is a
/// 400 field error with <see cref="ErrorCodes.ReportsInvalidFilter"/>, all reported together.
/// </summary>
internal static class ReportFilterBinder
{
    public static ReportFilter Bind(IQueryCollection query)
    {
        var failures = new List<ValidationFailure>();
        var filter = new ReportFilter
        {
            From = Date(query, ReportFilterKeys.From, failures),
            To = Date(query, ReportFilterKeys.To, failures),
            Kind = Enum<TicketKind>(query, ReportFilterKeys.Kind, failures),
            Priority = Enum<Priority>(query, ReportFilterKeys.Priority, failures),
            CategoryId = Id(query, ReportFilterKeys.CategoryId, failures),
            TeamId = Id(query, ReportFilterKeys.TeamId, failures),
            AssigneeId = Id(query, ReportFilterKeys.AssigneeId, failures),
            DepartmentId = Id(query, ReportFilterKeys.DepartmentId, failures),
            LocationId = Id(query, ReportFilterKeys.LocationId, failures),
            Status = Enum<TicketStatus>(query, ReportFilterKeys.Status, failures),
            SlaState = Enum<SlaStateFilter>(query, ReportFilterKeys.SlaState, failures),
            AssetType = Enum<AssetType>(query, ReportFilterKeys.AssetType, failures),
            AssetStatus = Enum<AssetStatus>(query, ReportFilterKeys.AssetStatus, failures),
        };

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        filter.EnsureValid();
        return filter;
    }

    private static string? Raw(IQueryCollection query, string key) =>
        query.TryGetValue(key, out var values) && !string.IsNullOrWhiteSpace(values.ToString()) ? values.ToString() : null;

    private static void Fail(List<ValidationFailure> failures, string key) =>
        failures.Add(new ValidationFailure(key, "Invalid value.") { ErrorCode = ErrorCodes.ReportsInvalidFilter });

    private static DateOnly? Date(IQueryCollection query, string key, List<ValidationFailure> failures)
    {
        if (Raw(query, key) is not { } raw)
        {
            return null;
        }

        if (DateOnly.TryParseExact(raw, ReportFilterKeys.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        Fail(failures, key);
        return null;
    }

    private static Guid? Id(IQueryCollection query, string key, List<ValidationFailure> failures)
    {
        if (Raw(query, key) is not { } raw)
        {
            return null;
        }

        if (Guid.TryParse(raw, out var id))
        {
            return id;
        }

        Fail(failures, key);
        return null;
    }

    private static TEnum? Enum<TEnum>(IQueryCollection query, string key, List<ValidationFailure> failures)
        where TEnum : struct, Enum
    {
        if (Raw(query, key) is not { } raw)
        {
            return null;
        }

        foreach (var candidate in System.Enum.GetValues<TEnum>())
        {
            if (string.Equals(ReportFilter.EnumName(candidate), raw, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        Fail(failures, key);
        return null;
    }
}
