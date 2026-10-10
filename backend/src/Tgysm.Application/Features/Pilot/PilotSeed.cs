using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Pilot;

/// <summary>Full-scale volumes of the pilot seed (spec §5.1); <c>seed-pilot --scale</c> multiplies them.</summary>
public static class PilotSeedVolumes
{
    public const int Employees = 50;
    public const int ItStaff = 8;

    /// <summary>Department approvers; an approver must be an admin (spec §3.4).</summary>
    public const int Approvers = 2;

    public const int Assets = 500;
    public const int Tickets = 2_000;
    public const int DaysBack = 90;
    public const int Articles = 20;

    /// <summary>A scaled volume, never below one so every kind of data exists at any scale.</summary>
    public static int Scaled(int volume, double scale) =>
        Math.Max(1, (int)Math.Round(volume * scale, MidpointRounding.AwayFromZero));
}

/// <summary>What one seed run created; printed by the command.</summary>
public sealed record PilotSeedResult(
    string AdminEmail,
    bool AdminCreated,
    string Password,
    IReadOnlyDictionary<UserRole, int> UsersByRole,
    int Assets,
    IReadOnlyDictionary<TicketStatus, int> TicketsByStatus,
    int Comments,
    int Articles,
    int AuditEvents);

/// <summary>
/// Fills an empty database with pilot data (spec §5.1) through the application handlers, so business rules, SLA
/// instances, timeline events, notifications and audit rows come out exactly as in real use. Each call runs in its
/// own DI scope with <see cref="PilotSeedActor"/> and <see cref="PilotSeedClock"/> set to who acts and when, so the
/// data spreads over the past <see cref="PilotSeedVolumes.DaysBack"/> days. Draws come from
/// <c>Random(<see cref="RandomSeed"/>)</c>: the same scale gives the same data.
/// </summary>
public sealed class PilotSeed(IServiceScopeFactory scopes, PilotSeedActor actor, PilotSeedClock clock, ITemporaryPasswordGenerator passwords)
{
    public const int RandomSeed = 20_261_009;

    /// <summary>Incident category in which pilot users report problems with TGYSM itself (user guide "Sorun bildirme").</summary>
    public const string PilotCategoryName = "TGYSM Pilot";

    /// <summary>Reserved domain (RFC 2606): notification mail for seeded accounts never reaches a real mailbox.</summary>
    public const string EmailDomain = "pilot.example.com";

    /// <summary>The seed only fills a database without tickets; it never mixes with real work.</summary>
    public async Task<bool> HasTicketsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAppDbContext>().Tickets.AnyAsync(cancellationToken);
    }

    public async Task<PilotSeedResult> RunAsync(double scale, CancellationToken cancellationToken)
    {
        var password = passwords.Generate();
        var session = new PilotSeedSession(scopes, actor, clock, scale, password, PilotSeedClock.RealUtcNow, cancellationToken);
        string adminEmail;
        bool adminCreated;
        try
        {
            (adminEmail, adminCreated) = await session.RunAsync();
        }
        finally
        {
            // A failed run keeps the tickets it committed; their notifications must not reach the Worker either.
            await CloseSeededNotificationsAsync(session.TicketIds, cancellationToken);
        }

        return await SummarizeAsync(adminEmail, adminCreated, password, cancellationToken);
    }

    /// <summary>
    /// The handlers queued notifications for every seeded ticket action; none of them must reach a mail relay. One
    /// conditional update moves only this run's still-pending rows to <see cref="OutboxStatus.Skipped"/> (the
    /// dispatcher's "nothing to send" end state), so the Worker never picks them up.
    /// </summary>
    private async Task CloseSeededNotificationsAsync(IReadOnlyList<Guid> ticketIds, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var now = PilotSeedClock.RealUtcNow;
        await db.OutboxEvents
            .Where(e => e.Status == OutboxStatus.Pending && e.TicketId != null && ticketIds.Contains(e.TicketId.Value))
            .ExecuteUpdateAsync(set => set.SetProperty(e => e.Status, OutboxStatus.Skipped).SetProperty(e => e.DispatchedAt, now), cancellationToken);
    }

    private async Task<PilotSeedResult> SummarizeAsync(string adminEmail, bool adminCreated, string password, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var users = await db.Users.GroupBy(u => u.Role).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        var tickets = await db.Tickets.GroupBy(t => t.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(cancellationToken);
        return new PilotSeedResult(
            adminEmail,
            adminCreated,
            password,
            users.ToDictionary(u => u.Key, u => u.Count),
            await db.Assets.CountAsync(cancellationToken),
            tickets.ToDictionary(t => t.Key, t => t.Count),
            await db.TicketComments.CountAsync(cancellationToken),
            await db.KnowledgeArticles.CountAsync(cancellationToken),
            await db.AuditEvents.CountAsync(cancellationToken));
    }
}
