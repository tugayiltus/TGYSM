using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Abstractions;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Reports;

/// <summary>The signed-in user of a report test, without HTTP.</summary>
public sealed class FixedActor(Guid? userId) : ICurrentActor
{
    public Guid? UserId { get; } = userId;

    public string CorrelationId => "test";
}

/// <summary>Two teams, an admin, IT staff in each team and an employee, for report scope and filter tests.</summary>
public sealed class ReportWorld
{
    private int _counter;

    private ReportWorld(ApiFactory factory) => Factory = factory;

    public ApiFactory Factory { get; }
    public User Admin { get; private set; } = null!;
    public User Desk { get; private set; } = null!;
    public User Field { get; private set; } = null!;
    public User Employee { get; private set; } = null!;
    public Team DeskTeam { get; private set; } = null!;
    public Team FieldTeam { get; private set; } = null!;

    public static async Task<ReportWorld> CreateAsync(ApiFactory factory)
    {
        var world = new ReportWorld(factory)
        {
            Admin = await TestUsers.CreateAsync(factory, UserRole.Admin, "admin@example.com"),
            Desk = await TestUsers.CreateAsync(factory, UserRole.ItStaff, "desk@example.com"),
            Field = await TestUsers.CreateAsync(factory, UserRole.ItStaff, "field@example.com"),
            Employee = await TestUsers.CreateAsync(factory, UserRole.Employee, "emp@example.com"),
        };
        world.DeskTeam = await TestData.TeamAsync(factory, "Masa", true, world.Desk.Id);
        world.FieldTeam = await TestData.TeamAsync(factory, "Saha", false, world.Field.Id);
        return world;
    }

    public AppDbContext Db() => Factory.Database.CreateDbContext();

    public async Task<Ticket> TicketAsync(Guid requesterId, Guid teamId, DateTimeOffset? createdAt = null, Action<Ticket>? change = null)
    {
        var ticket = Ticket.CreateIncident(
            $"INC-2026-{++_counter:D6}", "Konu", "Aciklama", null, null, requesterId, teamId, createdAt ?? DateTimeOffset.UtcNow);
        change?.Invoke(ticket);
        await using var db = Db();
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return ticket;
    }
}
