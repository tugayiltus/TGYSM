using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Sla;

namespace Tgysm.Application.Sla;

/// <summary>
/// Re-plans running SLA timers when the company calendar or a holiday changes (owner decision, final review F3):
/// elapsed business time up to now counts under the old rules, the remaining time is projected under the new ones.
/// Paused and stopped timers are left alone. Callers run <see cref="RunAsync"/> as their whole unit of work, so the
/// calendar change, the recomputed instances and their <see cref="SlaEventKind.CalendarChanged"/> events commit in one save.
/// </summary>
public sealed class SlaCalendarReplanner(IAppDbContext db, CalendarProvider calendar, TimeProvider clock)
{
    /// <summary>
    /// In one transaction: locks every unstopped instance (so a concurrent ticket action's write waits and this run
    /// reads its committed state), reads the current rules, lets <paramref name="change"/> apply the calendar change
    /// and return the new rules, recomputes the running instances, and saves once through <paramref name="save"/>.
    /// </summary>
    public async Task RunAsync(
        Func<BusinessCalendarRules, CancellationToken, Task<BusinessCalendarRules>> change,
        Func<CancellationToken, Task> save,
        CancellationToken cancellationToken)
    {
        await db.RunInTransactionAsync(async ct =>
        {
            await db.SlaInstances
                .Where(i => i.StoppedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.RunningSince, i => i.RunningSince), ct);

            var oldRules = await calendar.GetRulesAsync(ct);
            var newRules = await change(oldRules, ct);
            var now = clock.GetUtcNow();
            var running = await db.SlaInstances.Where(i => i.StoppedAt == null && i.RunningSince != null).ToListAsync(ct);
            foreach (var instance in running)
            {
                instance.ApplyCalendarChange(now, oldRules, newRules);
            }

            await save(ct);
        }, cancellationToken);
    }
}
