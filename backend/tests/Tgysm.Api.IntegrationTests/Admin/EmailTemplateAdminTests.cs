using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Notifications;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class EmailTemplateAdminTests(ApiFactory factory) : IAsyncLifetime
{
    private const string TemplatesPath = "/api/v1/admin/email-templates";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<ApiClient> AdminAsync() =>
        (await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com")).Client;

    private async Task<JsonElement> TicketCreatedTemplateAsync(ApiClient admin)
    {
        using var response = await admin.GetAsync(TemplatesPath);
        var items = await admin.ReadJsonAsync<JsonElement>(response);
        return items.EnumerateArray().Single(t => t.GetProperty("eventType").GetString() == NotificationEventTypes.TicketCreated);
    }

    [Fact]
    public async Task Admin_lists_the_ten_seeded_templates()
    {
        var admin = await AdminAsync();

        using var response = await admin.GetAsync(TemplatesPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await admin.ReadJsonAsync<JsonElement>(response)).EnumerateArray().ToList();
        Assert.Equal(10, items.Count);
        var eventTypes = items.Select(t => t.GetProperty("eventType").GetString()!).ToHashSet(StringComparer.Ordinal);
        var expectedEventTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            NotificationEventTypes.TicketCreated, NotificationEventTypes.TicketAssigned, NotificationEventTypes.PublicReply,
            NotificationEventTypes.ApprovalRequested, NotificationEventTypes.ApprovalDecided, NotificationEventTypes.TicketCompleted,
            NotificationEventTypes.SlaWarning, NotificationEventTypes.SlaBreached, NotificationEventTypes.AccountActivation,
            NotificationEventTypes.PasswordReset,
        };
        Assert.Equal(expectedEventTypes, eventTypes);
        Assert.All(items, t => Assert.False(string.IsNullOrWhiteSpace(t.GetProperty("subject").GetString())));
        Assert.All(items, t => Assert.False(string.IsNullOrWhiteSpace(t.GetProperty("body").GetString())));
    }

    [Fact]
    public async Task Every_seeded_template_uses_only_variables_allowed_for_its_event()
    {
        var admin = await AdminAsync();

        using var response = await admin.GetAsync(TemplatesPath);
        var items = (await admin.ReadJsonAsync<JsonElement>(response)).EnumerateArray().ToList();

        Assert.All(items, t =>
        {
            var eventType = t.GetProperty("eventType").GetString()!;
            Assert.Empty(TemplateRenderer.Validate(t.GetProperty("subject").GetString()!, eventType));
            Assert.Empty(TemplateRenderer.Validate(t.GetProperty("body").GetString()!, eventType));
        });
    }

    [Fact]
    public async Task Update_saves_the_new_wording_and_is_audited()
    {
        var admin = await AdminAsync();
        var template = await TicketCreatedTemplateAsync(admin);
        var path = $"{TemplatesPath}/{NotificationEventTypes.TicketCreated}";

        using var response = await admin.PutAsync(path, new
        {
            version = template.GetProperty("version").GetUInt32(),
            subject = "[{{ticketNumber}}] Yeni kaydınız",
            body = "Sayın {{recipientName}}, kaydınız açıldı. Durum: {{status}}",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await admin.ReadJsonAsync<JsonElement>(response);
        Assert.Equal("[{{ticketNumber}}] Yeni kaydınız", updated.GetProperty("subject").GetString());

        await using var db = factory.Database.CreateDbContext();
        var actions = await db.AuditEvents
            .Where(a => a.EntityId == template.GetProperty("id").GetGuid())
            .Select(a => a.Action)
            .ToListAsync(Ct);
        Assert.Contains(AuditActions.EmailTemplateUpdated, actions);
    }

    [Fact]
    public async Task Update_with_an_unknown_variable_returns_a_400_field_error()
    {
        var admin = await AdminAsync();
        var template = await TicketCreatedTemplateAsync(admin);
        var path = $"{TemplatesPath}/{NotificationEventTypes.TicketCreated}";

        using var response = await admin.PutAsync(path, new
        {
            version = template.GetProperty("version").GetUInt32(),
            subject = "[{{ticketNumber}}] {{thisDoesNotExist}}",
            body = "Merhaba {{recipientName}}",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fieldErrors = await ApiClient.ReadFieldCodesAsync(response);
        Assert.Contains(ErrorCodes.EmailTemplateUnknownVariable, fieldErrors["subject"]);
    }

    [Fact]
    public async Task Update_with_a_variable_not_allowed_for_this_event_returns_a_400_field_error()
    {
        var admin = await AdminAsync();
        var template = await TicketCreatedTemplateAsync(admin);
        var path = $"{TemplatesPath}/{NotificationEventTypes.TicketCreated}";

        // actionUrl is a known variable, but only for account events, not ticket_created.
        using var response = await admin.PutAsync(path, new
        {
            version = template.GetProperty("version").GetUInt32(),
            subject = "[{{ticketNumber}}] Kayıt",
            body = "Bağlantı: {{actionUrl}}",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fieldErrors = await ApiClient.ReadFieldCodesAsync(response);
        Assert.Contains(ErrorCodes.EmailTemplateVariableNotAllowed, fieldErrors["body"]);
    }

    [Fact]
    public async Task Stale_version_returns_conflict()
    {
        var admin = await AdminAsync();
        var template = await TicketCreatedTemplateAsync(admin);
        var path = $"{TemplatesPath}/{NotificationEventTypes.TicketCreated}";
        var version = template.GetProperty("version").GetUInt32();
        var body = new { version, subject = "[{{ticketNumber}}] Kayıt", body = "Merhaba {{recipientName}}" };

        using var first = await admin.PutAsync(path, body);
        using var second = await admin.PutAsync(path, body);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(second));
    }

    [Fact]
    public async Task It_staff_cannot_manage_email_templates()
    {
        var (it, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");

        using var response = await it.GetAsync(TemplatesPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
