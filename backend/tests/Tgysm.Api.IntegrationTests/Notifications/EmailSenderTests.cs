using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Notifications;
using Tgysm.Domain.Notifications;
using Tgysm.Infrastructure.Email;

namespace Tgysm.Api.IntegrationTests.Notifications;

/// <summary>
/// <see cref="SendDueEmailsHandler"/> (ADR-0014, ADR-0015, spec §3.2 step 3): retry back-off through to the
/// terminal <c>failed</c> status, admin retry, pickup-mode file output, account-link secret/body clearing, safety
/// under two concurrent sender runs, and that a decoupled SMTP outage never touches the triggering request.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class EmailSenderTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        factory.EmailSender.Reset();
    }

    public ValueTask DisposeAsync()
    {
        factory.EmailSender.Reset();
        return ValueTask.CompletedTask;
    }

    private async Task<int> DispatchOutboxAsync(int batch = 50)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<DispatchOutboxHandler>();
        return await handler.HandleAsync(batch, Lease, Ct);
    }

    private async Task<int> SendDueEmailsAsync(int batch = 50)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<SendDueEmailsHandler>();
        return await handler.HandleAsync(batch, Lease, Ct);
    }

    private async Task<EmailDelivery> SingleDeliveryAsync()
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.EmailDeliveries.SingleAsync(Ct);
    }

    private static async Task ForceDueNowAsync(ApiFactory apiFactory, Guid deliveryId)
    {
        await using var db = apiFactory.Database.CreateDbContext();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE email_deliveries SET next_attempt_at = now() - interval '1 hour' WHERE id = {deliveryId}", Ct);
    }

    [Fact]
    public async Task Failure_increments_attempts_and_schedules_back_off()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee);
        await DispatchOutboxAsync();
        factory.EmailSender.FailWith = _ => new InvalidOperationException("smtp down");

        await SendDueEmailsAsync();

        var delivery = await SingleDeliveryAsync();
        Assert.Equal(1, delivery.Attempts);
        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
        Assert.True(delivery.NextAttemptAt > DateTimeOffset.UtcNow.AddSeconds(30));
        // FakeEmailSender throws the exception verbatim (it stands in for an already-sanitized real sender, see
        // Last_error_never_contains_the_smtp_password for ErrorSanitizer's own coverage), so the stored last_error
        // is exactly the fake's message.
        Assert.Equal("smtp down", delivery.LastError);
    }

    [Fact]
    public async Task Five_failures_move_the_delivery_to_failed()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee);
        await DispatchOutboxAsync();
        factory.EmailSender.FailWith = _ => new InvalidOperationException("smtp down");

        Guid deliveryId = Guid.Empty;
        for (var attempt = 1; attempt <= RetryBackoff.MaxAttempts; attempt++)
        {
            await SendDueEmailsAsync();
            var delivery = await SingleDeliveryAsync();
            deliveryId = delivery.Id;
            Assert.Equal(attempt, delivery.Attempts);

            if (attempt < RetryBackoff.MaxAttempts)
            {
                Assert.Equal(DeliveryStatus.Pending, delivery.Status);
                await ForceDueNowAsync(factory, delivery.Id);
            }
            else
            {
                Assert.Equal(DeliveryStatus.Failed, delivery.Status);
            }
        }

        Assert.NotEqual(Guid.Empty, deliveryId);
    }

    [Fact]
    public async Task Admin_retry_moves_a_failed_delivery_to_pending_and_the_next_run_sends_it()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee);
        await DispatchOutboxAsync();
        factory.EmailSender.FailWith = _ => new InvalidOperationException("smtp down");
        for (var attempt = 1; attempt <= RetryBackoff.MaxAttempts; attempt++)
        {
            await SendDueEmailsAsync();
            if (attempt < RetryBackoff.MaxAttempts)
            {
                var delivery = await SingleDeliveryAsync();
                await ForceDueNowAsync(factory, delivery.Id);
            }
        }

        var failed = await SingleDeliveryAsync();
        Assert.Equal(DeliveryStatus.Failed, failed.Status);

        using var retryResponse = await world.Admin.PostAsync($"/api/v1/admin/email-deliveries/{failed.Id}/retry");
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);

        var afterRetry = await SingleDeliveryAsync();
        Assert.Equal(DeliveryStatus.Pending, afterRetry.Status);
        Assert.Equal(0, afterRetry.Attempts);

        factory.EmailSender.FailWith = null;
        await SendDueEmailsAsync();

        var sent = await SingleDeliveryAsync();
        Assert.Equal(DeliveryStatus.Sent, sent.Status);
        Assert.NotNull(sent.SentAt);
    }

    [Fact]
    public async Task Retrying_a_delivery_that_is_not_failed_returns_conflict()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee);
        await DispatchOutboxAsync();
        var pending = await SingleDeliveryAsync();

        using var response = await world.Admin.PostAsync($"/api/v1/admin/email-deliveries/{pending.Id}/retry");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(Tgysm.Domain.Common.ErrorCodes.EmailDeliveryNotFailed, await ApiClient.ReadCodeAsync(response));
    }

    [Fact]
    public async Task It_staff_cannot_retry_email_deliveries()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee);
        await DispatchOutboxAsync();
        var pending = await SingleDeliveryAsync();

        using var response = await world.Desk.PostAsync($"/api/v1/admin/email-deliveries/{pending.Id}/retry");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_lists_deliveries_filtered_by_status()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee);
        await DispatchOutboxAsync();

        using var response = await world.Admin.GetAsync("/api/v1/admin/email-deliveries?status=pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await world.Admin.ReadJsonAsync<System.Text.Json.JsonElement>(response);
        Assert.True(page.GetProperty("totalCount").GetInt32() >= 1);
        Assert.All(page.GetProperty("items").EnumerateArray(), item => Assert.Equal("pending", item.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task Pickup_mode_writes_one_eml_with_utf8_turkish_subject()
    {
        var folder = Path.Combine(Path.GetTempPath(), "tgysm-pickup-" + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new ServiceCollection();
            services.AddOptions<EmailOptions>().Configure(o =>
            {
                o.Mode = EmailModes.Pickup;
                o.From = "TGYSM <no-reply@tgysm.local>";
                o.PickupFolder = folder;
            });
            await using var provider = services.BuildServiceProvider();
            var sender = new PickupFolderEmailSender(provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<EmailOptions>>());
            const string turkishSubject = "[TGY-000123] Yazıcı arızası şöyle çözüldü";

            await sender.SendAsync(new EmailMessage("user@example.com", "Ayşe Yılmaz", turkishSubject, "Gövde metni"), Ct);

            var files = Directory.GetFiles(folder, "*.eml");
            var single = Assert.Single(files);
            var loaded = await MimeMessage.LoadAsync(single, Ct);
            Assert.Equal(turkishSubject, loaded.Subject);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Account_email_renders_the_action_url_then_clears_body_and_secret_after_sending()
    {
        var world = await TicketWorld.CreateAsync(factory);
        const string rawToken = "raw-activation-token-value";

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector(OutboxEvent.ProtectedSecretPurpose);
            var protectedSecret = protector.Protect(rawToken);
            await TestData.SaveAsync(factory, db =>
                db.OutboxEvents.Add(OutboxEvent.Create(NotificationEventTypes.AccountActivation, null, world.EmployeeUser.Id, "{}", protectedSecret, DateTimeOffset.UtcNow)));
        }

        await DispatchOutboxAsync();
        var beforeSend = await SingleDeliveryAsync();
        Assert.Null(beforeSend.Body);

        await SendDueEmailsAsync();

        var sentMessage = Assert.Single(factory.EmailSender.Sent);
        var expectedActionUrl = AccountLinkUrl.Build(ApiFactory.PublicBaseUrl, rawToken);
        Assert.Contains(expectedActionUrl, sentMessage.Body, StringComparison.Ordinal);

        var afterSend = await SingleDeliveryAsync();
        Assert.Equal(DeliveryStatus.Sent, afterSend.Status);
        Assert.Equal(EmailDelivery.ClearedBodyMarker, afterSend.Body);

        await using var db = factory.Database.CreateDbContext();
        var outboxEvent = await db.OutboxEvents.SingleAsync(e => e.EventType == NotificationEventTypes.AccountActivation, Ct);
        Assert.Null(outboxEvent.ProtectedSecret);
    }

    [Fact]
    public async Task Two_parallel_sender_runs_send_each_delivery_once()
    {
        const int ticketCount = 10;
        var world = await TicketWorld.CreateAsync(factory);
        for (var i = 0; i < ticketCount; i++)
        {
            await TicketWorld.CreateIncidentAsync(world.Employee, subject: $"Ağ sorunu {i}");
        }

        await DispatchOutboxAsync(ticketCount);

        var results = await Task.WhenAll(SendDueEmailsAsync(ticketCount), SendDueEmailsAsync(ticketCount));

        Assert.Equal(ticketCount, results.Sum());
        Assert.Equal(ticketCount, factory.EmailSender.Sent.Count);

        await using var db = factory.Database.CreateDbContext();
        var sentCount = await db.EmailDeliveries.CountAsync(d => d.Status == DeliveryStatus.Sent, Ct);
        Assert.Equal(ticketCount, sentCount);
    }

    [Fact]
    public async Task Queuing_a_ticket_action_succeeds_even_though_the_sender_is_currently_failing()
    {
        factory.EmailSender.FailWith = _ => new InvalidOperationException("smtp outage");
        var world = await TicketWorld.CreateAsync(factory);

        // TicketWorld.CreateIncidentAsync itself asserts 201 Created — the point of this test is that it stays
        // 201 even though the email pipeline (a later, decoupled step) is currently failing every send.
        await TicketWorld.CreateIncidentAsync(world.Employee);

        await DispatchOutboxAsync();
        await SendDueEmailsAsync();

        var delivery = await SingleDeliveryAsync();
        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
        Assert.Equal(1, delivery.Attempts);
    }

    [Fact]
    public void Last_error_never_contains_the_smtp_password()
    {
        const string password = "Sup3rSecretPa55w0rd!";
        var exception = new InvalidOperationException($"Authentication failed; password={password}");

        var sanitized = ErrorSanitizer.Sanitize(exception);

        Assert.DoesNotContain(password, sanitized, StringComparison.Ordinal);
        Assert.Contains("[redacted]", sanitized, StringComparison.Ordinal);
    }
}
