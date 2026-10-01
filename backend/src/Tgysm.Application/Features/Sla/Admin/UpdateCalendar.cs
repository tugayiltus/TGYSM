using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Sla;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Sla;

namespace Tgysm.Application.Features.Sla.Admin;

/// <summary><paramref name="Hours"/> replaces the whole week: a weekday not listed becomes closed.</summary>
public sealed record UpdateCalendarCommand(uint Version, string TimeZone, IReadOnlyList<WorkingHoursItem> Hours);

internal sealed class UpdateCalendarValidator : AbstractValidator<UpdateCalendarCommand>
{
    public UpdateCalendarValidator()
    {
        RuleFor(c => c.TimeZone).NotEmpty().Must(IanaTimeZone.IsValid).WithErrorCode(ErrorCodes.SlaInvalidTimeZone);
        RuleFor(c => c.Hours).NotNull();
        RuleFor(c => c.Hours)
            .Must(hours => hours.All(h => h.StartsAt < h.EndsAt) && hours.Select(h => h.Weekday).Distinct().Count() == hours.Count)
            .WithErrorCode(ErrorCodes.SlaInvalidHours)
            .When(c => c.Hours is not null);
        RuleFor(c => c.Hours).NotEmpty().WithErrorCode(ErrorCodes.SlaCalendarEmpty).When(c => c.Hours is not null);
    }
}

/// <summary>
/// Changes the calendar's zone and weekly hours (ADR-0018). Running SLA timers are re-planned in the same save:
/// time already run counts under the old calendar, the rest under the new one.
/// </summary>
public sealed class UpdateCalendarHandler(
    IAppDbContext db,
    IValidator<UpdateCalendarCommand> validator,
    ICurrentActor actor,
    IAuditLog audit,
    TimeProvider clock,
    SlaCalendarReplanner replanner)
{
    public async Task<CalendarDetail> HandleAsync(UpdateCalendarCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var calendar = await db.BusinessCalendars.SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.SlaCalendar);
        db.SetExpectedVersion(calendar, command.Version);
        var actorId = actor.UserId ?? throw new AuthenticationFailedException();

        await replanner.RunAsync(
            async (rules, ct) =>
            {
                var existing = await db.BusinessHours.ToListAsync(ct);
                var before = Snapshot(calendar.TimeZoneId, existing.Select(h => new WorkingHoursItem(h.Weekday, h.StartsAt, h.EndsAt)));
                calendar.Update(command.TimeZone, actorId, clock.GetUtcNow());
                ApplyHours(existing, command.Hours);
                audit.Record(AuditActions.SlaCalendarUpdated, AuditEntityTypes.SlaCalendar, calendar.Id, before, Snapshot(command.TimeZone, command.Hours));
                return rules with
                {
                    Zone = TimeZoneInfo.FindSystemTimeZoneById(command.TimeZone),
                    Hours = command.Hours.ToDictionary(h => h.Weekday, h => new WorkingRange(h.StartsAt, h.EndsAt)),
                };
            },
            db.SaveChangesAsync,
            cancellationToken);
        return await CalendarQueries.LoadAsync(db, cancellationToken);
    }

    private void ApplyHours(List<BusinessHours> existing, IReadOnlyList<WorkingHoursItem> wanted)
    {
        var wantedDays = wanted.Select(h => h.Weekday).ToHashSet();
        foreach (var closed in existing.Where(h => !wantedDays.Contains(h.Weekday)))
        {
            db.BusinessHours.Remove(closed);
        }

        foreach (var item in wanted)
        {
            var current = existing.SingleOrDefault(h => h.Weekday == item.Weekday);
            if (current is null)
            {
                db.BusinessHours.Add(BusinessHours.Create(item.Weekday, item.StartsAt, item.EndsAt));
            }
            else
            {
                current.SetRange(item.StartsAt, item.EndsAt);
            }
        }
    }

    private static object Snapshot(string timeZone, IEnumerable<WorkingHoursItem> hours) =>
        new { timeZone, hours = hours.OrderBy(h => h.Weekday).ToList() };
}
