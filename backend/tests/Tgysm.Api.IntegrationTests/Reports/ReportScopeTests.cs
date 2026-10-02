using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application.Common;
using Tgysm.Application.Reports;

namespace Tgysm.Api.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public sealed class ReportScopeTests(ApiFactory factory) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ReportScope ScopeFor(Tgysm.Infrastructure.Persistence.AppDbContext db, Guid? userId) => new(db, new FixedActor(userId));

    [Fact]
    public async Task It_staff_sees_only_current_team_tickets_not_own_requests_elsewhere()
    {
        var world = await ReportWorld.CreateAsync(factory);
        var inTeam = await world.TicketAsync(world.Employee.Id, world.DeskTeam.Id);
        await world.TicketAsync(world.Employee.Id, world.FieldTeam.Id);
        await world.TicketAsync(world.Desk.Id, world.FieldTeam.Id); // own request, other team

        await using var db = world.Db();
        var ids = await (await ScopeFor(db, world.Desk.Id).TicketsAsync(Ct)).Select(t => t.Id).ToListAsync(Ct);

        Assert.Equal([inTeam.Id], ids);
    }

    [Fact]
    public async Task Team_filter_on_a_foreign_team_returns_no_rows()
    {
        var world = await ReportWorld.CreateAsync(factory);
        await world.TicketAsync(world.Employee.Id, world.FieldTeam.Id);

        await using var db = world.Db();
        var scoped = await ScopeFor(db, world.Desk.Id).TicketsAsync(Ct);
        var filtered = new TicketReportSource(db, new TestClock()).Apply(scoped, new ReportFilter { TeamId = world.FieldTeam.Id }, ReportDateBasis.Created);

        Assert.Empty(await filtered.ToListAsync(Ct));
    }

    [Fact]
    public async Task Admin_sees_all_tickets_and_assets_and_employee_is_forbidden()
    {
        var world = await ReportWorld.CreateAsync(factory);
        await world.TicketAsync(world.Employee.Id, world.DeskTeam.Id);
        await world.TicketAsync(world.Employee.Id, world.FieldTeam.Id);

        await using var db = world.Db();
        Assert.Equal(2, await (await ScopeFor(db, world.Admin.Id).TicketsAsync(Ct)).CountAsync(Ct));
        _ = await ScopeFor(db, world.Admin.Id).AssetsAsync(Ct);
        _ = await ScopeFor(db, world.Desk.Id).AssetsAsync(Ct);
        await Assert.ThrowsAsync<ForbiddenException>(() => ScopeFor(db, world.Employee.Id).TicketsAsync(Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => ScopeFor(db, world.Employee.Id).AssetsAsync(Ct));
    }

    [Fact]
    public async Task Viewer_for_refuses_employees_and_unknown_users_and_gives_team_ids()
    {
        var world = await ReportWorld.CreateAsync(factory);

        await using var db = world.Db();
        var scope = ScopeFor(db, null);
        var viewer = await scope.ViewerForAsync(world.Desk.Id, Ct);

        Assert.Equal([world.DeskTeam.Id], viewer.TeamIds);
        await Assert.ThrowsAsync<ForbiddenException>(() => scope.ViewerForAsync(world.Employee.Id, Ct));
        await Assert.ThrowsAsync<ForbiddenException>(() => scope.ViewerForAsync(Guid.NewGuid(), Ct));
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => scope.ViewerAsync(Ct));
    }
}
