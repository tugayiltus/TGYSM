namespace Tgysm.Application.Common;

/// <summary>
/// Cross-cutting application configuration read by both hosts (<c>Tgysm.Api</c>, <c>Tgysm.Worker</c>) under the
/// <c>App</c> section. <see cref="PublicBaseUrl"/> is the base for links rendered into email (spec §3.2, `ticketUrl`);
/// <see cref="TimeZone"/> is the company calendar's zone used by <see cref="CompanyClock"/> until the SLA calendar
/// (a later task) becomes its source.
/// </summary>
public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>IANA id; the company calendar's default until the SLA calendar supplies its own (spec §5.1).</summary>
    public const string DefaultTimeZone = "Europe/Istanbul";

    public string PublicBaseUrl { get; init; } = string.Empty;

    public string TimeZone { get; init; } = DefaultTimeZone;
}
