using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Tickets;

namespace Tgysm.Infrastructure.Persistence;

/// <summary>Counter upsert: <c>INSERT … ON CONFLICT DO UPDATE … RETURNING</c> (ADR-0011).</summary>
public sealed class TicketNumberGenerator(AppDbContext db) : ITicketNumberGenerator
{
    public async Task<long> NextAsync(TicketKind kind, int year, CancellationToken cancellationToken)
    {
        var kindName = JsonNamingPolicy.SnakeCaseLower.ConvertName(kind.ToString());
        var values = await db.Database.SqlQuery<long>(
                $"""
                INSERT INTO ticket_counters (kind, year, last_value) VALUES ({kindName}, {year}, 1)
                ON CONFLICT (kind, year) DO UPDATE SET last_value = ticket_counters.last_value + 1
                RETURNING last_value AS "Value"
                """)
            .ToListAsync(cancellationToken);
        return values[0];
    }
}
