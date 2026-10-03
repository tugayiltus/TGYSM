namespace Tgysm.Domain.Reports;

/// <summary>The reports of spec §7.2. Lives in Domain because an <see cref="ExportJob"/> stores which report it exports.</summary>
public enum ReportName
{
    Incidents,
    Requests,
    Operations,
    Transfers,
    Assets,
    Warranty,
}

/// <summary>Which half of a ratio a drill-down or export lists. Only ratios have a <see cref="Denominator"/>.</summary>
public enum ReportDrillPart
{
    Numerator,
    Denominator,
}
