using Tgysm.Domain.Sla;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Sla.Admin;

/// <summary>One weekday's working window; used for both input and output. A weekday not listed is closed.</summary>
public sealed record WorkingHoursItem(DayOfWeek Weekday, TimeOnly StartsAt, TimeOnly EndsAt);

public sealed record CalendarDetail(Guid Id, uint Version, string TimeZone, IReadOnlyList<WorkingHoursItem> Hours, DateTimeOffset UpdatedAt)
{
    public static CalendarDetail From(BusinessCalendar calendar, IEnumerable<BusinessHours> hours) =>
        new(calendar.Id, calendar.Version, calendar.TimeZoneId, ToItems(hours), calendar.UpdatedAt);

    private static List<WorkingHoursItem> ToItems(IEnumerable<BusinessHours> hours) =>
        hours.OrderBy(h => MondayFirst(h.Weekday)).Select(h => new WorkingHoursItem(h.Weekday, h.StartsAt, h.EndsAt)).ToList();

    private static int MondayFirst(DayOfWeek day) => ((int)day + (int)DayOfWeek.Saturday) % (int)(DayOfWeek.Saturday + 1);
}

public sealed record HolidayItem(Guid Id, DateOnly Date, string Name)
{
    public static HolidayItem From(Holiday holiday) => new(holiday.Id, holiday.Date, holiday.Name);
}

public sealed record PolicyTargetItem(TicketKind Kind, Priority Priority, Guid? RequestTypeId, SlaMetric Metric, int TargetMinutes, int WarningPercent);

public sealed record PauseRuleItem(HoldReason HoldReason, bool PausesFirstResponse, bool PausesResolution);

public sealed record PolicyDetail(
    Guid VersionId,
    int Number,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    IReadOnlyList<PolicyTargetItem> Targets,
    IReadOnlyList<PauseRuleItem> PauseRules)
{
    public static PolicyDetail From(SlaPolicyVersion version) =>
        new(
            version.Id,
            version.Number,
            version.CreatedAt,
            version.CreatedBy,
            version.Targets
                .OrderBy(t => t.RequestTypeId.HasValue)
                .ThenBy(t => t.Kind)
                .ThenBy(t => t.Priority)
                .ThenBy(t => t.Metric)
                .Select(t => new PolicyTargetItem(t.Kind, t.Priority, t.RequestTypeId, t.Metric, t.TargetMinutes, t.WarningPercent))
                .ToList(),
            version.PauseRules.OrderBy(r => r.HoldReason).Select(r => new PauseRuleItem(r.HoldReason, r.PausesFirstResponse, r.PausesResolution)).ToList());
}
