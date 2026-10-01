using Tgysm.Domain.Common;

namespace Tgysm.Domain.Sla;

/// <summary>The single company calendar row (spec §5.1): its IANA time zone. Weekly hours are <see cref="BusinessHours"/>, holidays are <see cref="Holiday"/>.</summary>
public sealed class BusinessCalendar : Entity
{
    private BusinessCalendar()
    {
    }

    public string TimeZoneId { get; private set; } = string.Empty;

    public Guid UpdatedBy { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static BusinessCalendar Create(string timeZoneId, Guid updatedBy, DateTimeOffset now)
    {
        var calendar = new BusinessCalendar();
        calendar.Update(timeZoneId, updatedBy, now);
        return calendar;
    }

    public void Update(string timeZoneId, Guid updatedBy, DateTimeOffset now)
    {
        if (!IanaTimeZone.IsValid(timeZoneId))
        {
            throw new DomainException(ErrorCodes.SlaInvalidTimeZone, "The time zone is not a known IANA identifier.");
        }

        TimeZoneId = timeZoneId;
        UpdatedBy = updatedBy;
        UpdatedAt = now;
    }
}
