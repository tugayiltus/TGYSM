using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Application.Notifications;
using Tgysm.Domain.Notifications;

namespace Tgysm.Api.IntegrationTests.Tasks;

[Collection(ApiCollection.Name)]
public sealed class TaskNotificationTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task DispatchAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DispatchOutboxHandler>().HandleAsync(50, Lease, Ct);
    }

    [Fact]
    public async Task Assignee_gets_a_task_mail_with_number_title_and_link()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Desk, world.FieldTeam.Id, world.FieldUser.Id, dueDate: "2026-12-01");

        await DispatchAsync();

        await using var db = factory.Database.CreateDbContext();
        var delivery = await db.EmailDeliveries.SingleAsync(Ct);
        Assert.Equal((world.FieldUser.Id, NotificationEventTypes.TaskAssigned), (delivery.RecipientUserId, delivery.TemplateType));
        Assert.Contains(TaskWorld.NumberOf(task), delivery.Subject, StringComparison.Ordinal);
        Assert.Contains("Yedek al", delivery.Body, StringComparison.Ordinal);
        Assert.Contains("Saha Destek", delivery.Body, StringComparison.Ordinal);
        Assert.Contains("01.12.2026", delivery.Body, StringComparison.Ordinal);
        Assert.Contains($"{ApiFactory.PublicBaseUrl}/tasks/{TaskWorld.NumberOf(task)}", delivery.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", delivery.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Transfer_without_assignee_mails_the_new_team_but_not_the_actor()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var task = await TaskWorld.CreateForTeamAsync(world.Field, world.FieldTeam.Id);
        using (var response = await world.Field.PostAsync(TaskWorld.PathOf(task, "transfer"), new { version = TaskWorld.VersionOf(task), teamId = world.ServiceDesk.Id, reason = "workload" }))
        {
            response.EnsureSuccessStatusCode();
        }

        await DispatchAsync();

        await using var db = factory.Database.CreateDbContext();
        var recipients = await db.EmailDeliveries.Select(d => d.RecipientUserId).ToListAsync(Ct);
        Assert.Equal([world.DeskUser.Id], recipients);
    }
}
