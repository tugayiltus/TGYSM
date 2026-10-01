namespace Tgysm.Domain.Sla;

/// <summary>
/// IANA time zone identifiers only (<c>Europe/Istanbul</c>, <c>UTC</c>). Windows ids ("Turkey Standard Time") and
/// legacy abbreviations ("EST") resolve on some hosts but not on others, so the calendar refuses them to stay portable.
/// </summary>
public static class IanaTimeZone
{
    public const int MaxLength = 64;
    private const string Utc = "UTC";
    private const char RegionSeparator = '/';
    private const char Space = ' ';

    public static bool IsValid(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > MaxLength || id.Contains(Space))
        {
            return false;
        }

        if (id != Utc && !id.Contains(RegionSeparator))
        {
            return false;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id).Id == id;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
