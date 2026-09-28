using System.Globalization;
using Microsoft.Extensions.Options;

namespace Tgysm.Application.Common;

/// <summary>
/// Formats an instant in the company's calendar time zone with Turkish conventions (spec §3.4: "dates render in
/// the company time zone with Turkish formatting"). A small interface so the SLA calendar (a later task) can
/// become the zone's source instead of configuration, without changing every caller.
/// </summary>
public interface ICompanyClock
{
    /// <summary>Converts <paramref name="instant"/> to the company zone and renders it as <c>dd.MM.yyyy HH:mm</c>, Turkish-formatted.</summary>
    string Format(DateTimeOffset instant);
}

/// <summary>Reads the zone from <c>App:TimeZone</c> (default <see cref="AppOptions.DefaultTimeZone"/>) until the SLA calendar supplies it.</summary>
internal sealed class CompanyClock(IOptionsMonitor<AppOptions> options) : ICompanyClock
{
    private const string DisplayFormat = "dd.MM.yyyy HH:mm";
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public string Format(DateTimeOffset instant)
    {
        var zoneId = string.IsNullOrWhiteSpace(options.CurrentValue.TimeZone) ? AppOptions.DefaultTimeZone : options.CurrentValue.TimeZone;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var local = TimeZoneInfo.ConvertTime(instant, zone);
        return local.ToString(DisplayFormat, TurkishCulture);
    }
}
