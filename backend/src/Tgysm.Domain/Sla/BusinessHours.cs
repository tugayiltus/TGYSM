using Tgysm.Domain.Common;

namespace Tgysm.Domain.Sla;

/// <summary>One weekday's working window in local time; a weekday without a row is closed. At most one row per weekday.</summary>
public sealed class BusinessHours : Entity
{
    private BusinessHours()
    {
    }

    public DayOfWeek Weekday { get; private init; }

    public TimeOnly StartsAt { get; private set; }

    public TimeOnly EndsAt { get; private set; }

    public static BusinessHours Create(DayOfWeek weekday, TimeOnly startsAt, TimeOnly endsAt)
    {
        var hours = new BusinessHours { Weekday = weekday };
        hours.SetRange(startsAt, endsAt);
        return hours;
    }

    public void SetRange(TimeOnly startsAt, TimeOnly endsAt)
    {
        if (startsAt >= endsAt)
        {
            throw new DomainException(ErrorCodes.SlaInvalidHours, "Working hours must start before they end.");
        }

        StartsAt = startsAt;
        EndsAt = endsAt;
    }
}
