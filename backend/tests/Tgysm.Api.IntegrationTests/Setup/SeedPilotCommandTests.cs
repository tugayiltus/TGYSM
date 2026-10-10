using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Tgysm.Api.Cli;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Application;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Organization;
using Tgysm.Application.Features.Pilot;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Catalog;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;
using Tgysm.Infrastructure;
using Tgysm.Infrastructure.Persistence;

namespace Tgysm.Api.IntegrationTests.Setup;

/// <summary>The <c>seed-pilot</c> command: refusal rules and a small-scale run through the application handlers.</summary>
[Collection(ApiCollection.Name)]
public sealed class SeedPilotCommandTests(ApiFactory factory) : IAsyncLifetime
{
    private const int CliSuccess = 0;
    private const int CliFailure = 1;
    private const string ProductionEnvironment = "Production";
    private const string SmallScale = "0.02";
    private const double SmallScaleFactor = 0.02;
    private const string ScaleOption = "--scale";
    private const string PasswordMarker = "(shown once): ";
    private const int TicketsBeforeFailure = 1;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private IServiceProvider ServicesIn(string environment) =>
        factory.WithWebHostBuilder(b => b.UseEnvironment(environment)).Services;

    [Fact]
    public async Task Refuses_in_production_and_writes_nothing()
    {
        var error = new StringWriter();

        var exit = await SeedPilotCommand.RunAsync(ServicesIn(ProductionEnvironment), [ScaleOption, SmallScale], TextWriter.Null, error);

        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(CliFailure, exit);
        Assert.Contains(ProductionEnvironment, error.ToString(), StringComparison.Ordinal);
        Assert.False(await db.Users.AnyAsync(Ct));
        Assert.False(await db.AuditEvents.AnyAsync(Ct));
    }

    [Fact]
    public async Task Refuses_when_any_ticket_exists_and_writes_nothing()
    {
        var requester = await TestUsers.CreateAsync(factory, UserRole.Employee, "existing@example.com");
        var team = await TestData.TeamAsync(factory, "Masa", true);
        await TestData.SaveAsync(factory, db => db.Tickets.Add(
            Ticket.CreateIncident("INC-2026-000001", "Konu", "Aciklama", null, null, requester.Id, team.Id, DateTimeOffset.UtcNow)));
        var error = new StringWriter();

        var exit = await SeedPilotCommand.RunAsync(ServicesIn(SeedPilotCommand.PilotEnvironment), [ScaleOption, SmallScale], TextWriter.Null, error);

        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(CliFailure, exit);
        Assert.Contains("tickets", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, await db.Users.CountAsync(Ct));
        Assert.False(await db.AuditEvents.AnyAsync(Ct));
    }

    [Fact]
    public async Task Rejects_an_invalid_scale()
    {
        var error = new StringWriter();

        var exit = await SeedPilotCommand.RunAsync(ServicesIn(SeedPilotCommand.PilotEnvironment), [ScaleOption, "0"], TextWriter.Null, error);

        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(CliFailure, exit);
        Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
        Assert.False(await db.Users.AnyAsync(Ct));
    }

    [Fact]
    public async Task Creates_the_scaled_pilot_data_through_the_handlers()
    {
        var output = new StringWriter();
        var services = ServicesIn(SeedPilotCommand.PilotEnvironment);
        var startedAt = DateTimeOffset.UtcNow;

        var exit = await SeedPilotCommand.RunAsync(services, [ScaleOption, SmallScale], output, TextWriter.Null);

        Assert.Equal(CliSuccess, exit);
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(Scaled(PilotSeedVolumes.Employees), await db.Users.CountAsync(u => u.Role == UserRole.Employee, Ct));
        Assert.Equal(Scaled(PilotSeedVolumes.ItStaff), await db.Users.CountAsync(u => u.Role == UserRole.ItStaff, Ct));
        Assert.True(await db.Users.AnyAsync(u => u.Role == UserRole.Admin, Ct));

        var assets = await db.Assets.AsNoTracking().ToListAsync(Ct);
        Assert.Equal(Scaled(PilotSeedVolumes.Assets), assets.Count);
        Assert.All(assets, a =>
        {
            Assert.NotEqual(Guid.Empty, a.ModelId);
            Assert.False(string.IsNullOrWhiteSpace(a.Brand));
            Assert.False(string.IsNullOrWhiteSpace(a.Model));
        });
        var modelIds = await db.AssetModels.Select(m => m.Id).ToListAsync(Ct);
        Assert.All(assets, a => Assert.Contains(a.ModelId, modelIds));

        var tickets = await db.Tickets.AsNoTracking().ToListAsync(Ct);
        Assert.Equal(Scaled(PilotSeedVolumes.Tickets), tickets.Count);
        var ticketsWithSla = await db.SlaInstances.Select(s => s.TicketId).Distinct().CountAsync(Ct);
        Assert.Equal(tickets.Count, ticketsWithSla);
        Assert.Equal(Enum.GetValues<TicketStatus>().ToHashSet(), tickets.Select(t => t.Status).ToHashSet());
        Assert.Contains(tickets, t => t.Kind == TicketKind.Request);
        Assert.Contains(tickets, t => t.Kind == TicketKind.Incident);
        var oldestAllowed = startedAt.AddDays(-PilotSeedVolumes.DaysBack - 1);
        Assert.All(tickets, t => Assert.InRange(t.CreatedAt, oldestAllowed, DateTimeOffset.UtcNow));
        Assert.True(tickets.Max(t => t.CreatedAt) - tickets.Min(t => t.CreatedAt) > TimeSpan.FromDays(PilotSeedVolumes.DaysBack / 2));

        Assert.Equal(tickets.Count, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.TicketCreated, Ct));
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.AssetCreated, Ct));
        Assert.True(await db.TicketComments.AnyAsync(Ct));
        Assert.Equal(Scaled(PilotSeedVolumes.Articles), await db.KnowledgeArticles.CountAsync(Ct));
        Assert.True(await db.Categories.AnyAsync(c => c.Name == PilotSeed.PilotCategoryName && c.Scope == CategoryScope.Incident, Ct));

        var printed = output.ToString();
        var password = PrintedPassword(printed);
        Assert.Equal(1, CountOccurrences(printed, password));
        var passwords = services.GetRequiredService<IPasswordService>();
        var employee = await db.Users.AsNoTracking().FirstAsync(u => u.Role == UserRole.Employee, Ct);
        Assert.True(passwords.Verify(employee.PasswordHash, password));
        Assert.False(employee.MustChangePassword);
    }

    [Fact]
    public async Task Seeds_location_names_the_csv_importer_can_address()
    {
        var exit = await SeedPilotCommand.RunAsync(ServicesIn(SeedPilotCommand.PilotEnvironment), [ScaleOption, SmallScale], TextWriter.Null, TextWriter.Null);

        Assert.Equal(CliSuccess, exit);
        await using var db = factory.Database.CreateDbContext();
        var names = await db.Locations.AsNoTracking().Select(l => l.Name).ToListAsync(Ct);
        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.DoesNotContain(LocationPaths.SegmentSeparator, name));
        Assert.Contains(await db.Locations.AsNoTracking().ToListAsync(Ct), l => l.ParentId is not null);
    }

    [Fact]
    public async Task Leaves_no_seeded_notification_for_the_worker_to_send()
    {
        // The seed writes outbox rows through the handlers; a pilot Worker on a real relay must never mail them.
        var exit = await SeedPilotCommand.RunAsync(ServicesIn(SeedPilotCommand.PilotEnvironment), [ScaleOption, SmallScale], TextWriter.Null, TextWriter.Null);

        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(CliSuccess, exit);
        Assert.True(await db.OutboxEvents.AnyAsync(Ct));
        Assert.False(await db.OutboxEvents.AnyAsync(e => e.Status == OutboxStatus.Pending, Ct));
        Assert.All(await db.OutboxEvents.AsNoTracking().ToListAsync(Ct), e => Assert.NotNull(e.DispatchedAt));
    }

    [Fact]
    public async Task A_failed_seed_still_closes_the_notifications_it_queued()
    {
        // The second ticket fails after the first one committed its notifications; those must not reach the Worker.
        await using var services = SeedServicesWith(new FailingTicketAuditLog(TicketsBeforeFailure));

        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<PilotSeed>().RunAsync(SmallScaleFactor, Ct));

        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(TicketsBeforeFailure, await db.Tickets.CountAsync(Ct));
        Assert.True(await db.OutboxEvents.AnyAsync(Ct));
        Assert.False(await db.OutboxEvents.AnyAsync(e => e.Status == OutboxStatus.Pending, Ct));
    }

    [Fact]
    public async Task A_seed_failure_is_not_masked_when_the_run_is_also_cancelled()
    {
        // The run is cancelled as the seed fails (Ctrl+C): the seed's own exception reaches the caller, and the
        // notifications it queued are still closed.
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var services = SeedServicesWith(new FailingTicketAuditLog(TicketsBeforeFailure, cancellation));

        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<PilotSeed>().RunAsync(SmallScaleFactor, cancellation.Token));

        await using var db = factory.Database.CreateDbContext();
        Assert.True(await db.OutboxEvents.AnyAsync(Ct));
        Assert.False(await db.OutboxEvents.AnyAsync(e => e.Status == OutboxStatus.Pending, Ct));
    }

    [Fact]
    public async Task Prints_the_breach_mail_warning()
    {
        var output = new StringWriter();

        var exit = await SeedPilotCommand.RunAsync(ServicesIn(SeedPilotCommand.PilotEnvironment), [ScaleOption, SmallScale], output, TextWriter.Null);

        Assert.Equal(CliSuccess, exit);
        Assert.Contains(PilotSeed.EmailDomain, output.ToString(), StringComparison.Ordinal);
        Assert.Contains("EMAIL_MODE=Pickup", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_same_seed_produces_the_same_data()
    {
        var services = ServicesIn(SeedPilotCommand.PilotEnvironment);

        Assert.Equal(CliSuccess, await SeedPilotCommand.RunAsync(services, [ScaleOption, SmallScale], TextWriter.Null, TextWriter.Null));
        var first = await SnapshotAsync();
        await factory.ResetAsync();
        Assert.Equal(CliSuccess, await SeedPilotCommand.RunAsync(services, [ScaleOption, SmallScale], TextWriter.Null, TextWriter.Null));
        var second = await SnapshotAsync();

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }

    private static int Scaled(int volume) => PilotSeedVolumes.Scaled(volume, SmallScaleFactor);

    /// <summary>The seed command's composition (Application + Infrastructure, seed actor and clock) with another audit log.</summary>
    private ServiceProvider SeedServicesWith(IAuditLog audit)
    {
        var services = new ServiceCollection();
        services.AddSingleton(factory.Services.GetRequiredService<IConfiguration>());
        services.AddSingleton(factory.Services.GetRequiredService<IHostEnvironment>());
        services.AddLogging();
        services.AddSingleton<PilotSeedClock>();
        services.AddSingleton<TimeProvider>(provider => provider.GetRequiredService<PilotSeedClock>());
        services.AddSingleton<PilotSeedActor>();
        services.AddSingleton<ICurrentActor>(provider => provider.GetRequiredService<PilotSeedActor>());
        services.AddApplication();
        services.AddInfrastructure();
        services.AddSingleton(audit);
        services.AddSingleton<PilotSeed>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }

    private static string PrintedPassword(string output)
    {
        var start = output.IndexOf(PasswordMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "The pilot password is printed.");
        var rest = output[(start + PasswordMarker.Length)..];
        return rest[..rest.IndexOfAny(['\r', '\n'])];
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>Everything that does not depend on the moment of the run (ids, timestamps).</summary>
    private async Task<List<string>> SnapshotAsync()
    {
        await using AppDbContext db = factory.Database.CreateDbContext();
        var tickets = await (
            from ticket in db.Tickets
            join requester in db.Users on ticket.RequesterId equals requester.Id
            join assignee in db.Users on ticket.AssigneeId equals assignee.Id into assignees
            from assignee in assignees.DefaultIfEmpty()
            orderby ticket.Number
            select $"{ticket.Number}|{ticket.Kind}|{ticket.Subject}|{ticket.Status}|{ticket.Priority}|{requester.Email}|{(assignee == null ? "-" : assignee.Email)}")
            .ToListAsync(Ct);
        var assets = await db.Assets.OrderBy(a => a.SerialNumber)
            .Select(a => $"{a.SerialNumber}|{a.Brand}|{a.Model}|{a.Status}")
            .ToListAsync(Ct);
        var comments = await db.TicketComments.CountAsync(Ct);
        return [.. tickets, .. assets, $"comments={comments}"];
    }

    /// <summary>
    /// Records nothing and throws on the ticket creation after <paramref name="allowedTickets"/>, first cancelling
    /// <paramref name="cancelOnFailure"/> when given.
    /// </summary>
    private sealed class FailingTicketAuditLog(int allowedTickets, CancellationTokenSource? cancelOnFailure = null) : IAuditLog
    {
        private int _ticketsCreated;

        public void Record(string action, string entityType, Guid? entityId, object? oldValues = null, object? newValues = null, string? reason = null, Guid? actorUserId = null)
        {
            if (action == AuditActions.TicketCreated && Interlocked.Increment(ref _ticketsCreated) > allowedTickets)
            {
                cancelOnFailure?.Cancel();
                throw new InvalidOperationException("Simulated failure while seeding tickets.");
            }
        }
    }
}
