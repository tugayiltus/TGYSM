using FluentValidation;
using FluentValidation.Results;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Reports;

public enum ReportName
{
    Incidents,
    Requests,
    Operations,
    Transfers,
    Assets,
    Warranty,
}

/// <summary>Route values of <see cref="ReportName"/>: lower kebab-case ("incidents").</summary>
public static class ReportNames
{
    private const string ReportField = "report";

    public static string ToRoute(ReportName name) => name.ToString().ToLowerInvariant();

    /// <summary>Parses a route value; unknown names are a 400 with <see cref="ErrorCodes.ReportsUnknownReport"/>.</summary>
    public static ReportName Parse(string? route)
    {
        foreach (var name in Enum.GetValues<ReportName>())
        {
            if (string.Equals(ToRoute(name), route, StringComparison.Ordinal))
            {
                return name;
            }
        }

        throw new ValidationException([new ValidationFailure(ReportField, "Unknown report.") { ErrorCode = ErrorCodes.ReportsUnknownReport }]);
    }
}
