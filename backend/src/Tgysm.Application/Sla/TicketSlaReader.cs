using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Sla;

namespace Tgysm.Application.Sla;

/// <summary>One timer of a ticket as ticket DTOs show it (spec §5). <c>RemainingSeconds</c> is null once stopped, negative when overdue.</summary>
public sealed record TicketSla(
    SlaMetric Metric,
    DateTimeOffset? DueAt,
    long? RemainingSeconds,
    bool IsPaused,
    bool IsWarned,
    bool IsBreached,
    SlaResult? Result);

/// <summary>Builds the <c>sla</c> block for many tickets at once: instances and calendar rules are loaded once per call (no N+1).</summary>
public sealed class TicketSlaReader(IAppDbContext db, CalendarProvider calendar, TimeProvider clock)
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<TicketSla>>> LoadAsync(IReadOnlyCollection<Guid> ticketIds, CancellationToken cancellationToken)
    {
        if (ticketIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<TicketSla>>();
        }

        var instances = await db.SlaInstances.AsNoTracking().Where(i => ticketIds.Contains(i.TicketId)).ToListAsync(cancellationToken);
        var rules = instances.Any(i => i.RunningSince is not null) ? await calendar.GetRulesAsync(cancellationToken) : null;
        var now = clock.GetUtcNow();
        return instances
            .GroupBy(i => i.TicketId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<TicketSla>)[.. g.OrderBy(i => i.Metric).Select(i => new TicketSla(
                    i.Metric, i.DueAt, i.RemainingSeconds(now, rules), i.IsPaused, i.WarnedAt is not null, i.BreachedAt is not null, i.Result))]);
    }

    public async Task<IReadOnlyList<TicketSla>> LoadAsync(Guid ticketId, CancellationToken cancellationToken) =>
        (await LoadAsync([ticketId], cancellationToken)).GetValueOrDefault(ticketId) ?? [];
}
