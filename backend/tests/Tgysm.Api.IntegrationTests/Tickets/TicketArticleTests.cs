using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Knowledge;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Api.IntegrationTests.Tickets;

[Collection(ApiCollection.Name)]
public sealed class TicketArticleTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ArticlesSegment = "articles";
    private const string CommentsSegment = "comments";
    private const string BodyField = "body";
    private const string VisibilityField = "visibility";
    private const string Public = "public";
    private const string Internal = "internal";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static Guid IdOf(JsonElement article) => article.GetProperty("id").GetGuid();

    private static Task<HttpResponseMessage> LinkAsync(ApiClient client, JsonElement ticket, Guid articleId) =>
        client.PostAsync(TicketWorld.PathOf(ticket, ArticlesSegment), new { articleId });

    private static Task<HttpResponseMessage> ReplyAsync(ApiClient client, JsonElement ticket, string body, string visibility) =>
        client.PostMultipartAsync(TicketWorld.PathOf(ticket, CommentsSegment), new Dictionary<string, string> { [BodyField] = body, [VisibilityField] = visibility }, []);

    [Fact]
    public async Task IT_links_internal_and_public_article_and_requester_sees_only_the_public_one()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var publicArticle = await ArticleApiTests.PublishedAsync(world.Desk, "Herkese açık");
        var internalArticle = await ArticleApiTests.PublishedAsync(world.Desk, "Yalnızca BT", visibility: Internal);

        using var first = await LinkAsync(world.Desk, ticket, IdOf(publicArticle));
        using var second = await LinkAsync(world.Desk, ticket, IdOf(internalArticle));
        var desk = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(ticket, ArticlesSegment));
        var employee = await TicketWorld.ReadAsync(world.Employee, TicketWorld.PathOf(ticket, ArticlesSegment));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.StatusCode, second.StatusCode));
        Assert.Equal(2, desk.GetArrayLength());
        Assert.Equal(["Herkese açık"], employee.EnumerateArray().Select(i => i.GetProperty("title").GetString()!));
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(2, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.TicketArticleLinked, Ct));
    }

    [Fact]
    public async Task Linking_touches_updated_at_without_a_version_check_and_unlink_removes_it()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var article = await ArticleApiTests.PublishedAsync(world.Desk);
        var before = (await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(ticket))).GetProperty("updatedAt").GetDateTimeOffset();

        using var link = await LinkAsync(world.Desk, ticket, IdOf(article));
        var after = (await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(ticket))).GetProperty("updatedAt").GetDateTimeOffset();
        using var unlink = await world.Desk.DeleteAsync(TicketWorld.PathOf(ticket, $"{ArticlesSegment}/{IdOf(article)}"));

        Assert.True(after > before);
        Assert.Equal(HttpStatusCode.OK, unlink.StatusCode);
        Assert.Equal(0, (await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(ticket, ArticlesSegment))).GetArrayLength());
        await using var db = factory.Database.CreateDbContext();
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.TicketArticleUnlinked, Ct));
    }

    [Fact]
    public async Task Requester_of_another_ticket_and_IT_outside_the_team_get_404()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var article = await ArticleApiTests.PublishedAsync(world.Desk);

        using var otherList = await world.OtherEmployee.GetAsync(TicketWorld.PathOf(ticket, ArticlesSegment));
        using var otherLink = await LinkAsync(world.OtherEmployee, ticket, IdOf(article));
        using var fieldList = await world.Field.GetAsync(TicketWorld.PathOf(ticket, ArticlesSegment));
        using var fieldLink = await LinkAsync(world.Field, ticket, IdOf(article));

        Assert.All(new[] { otherList, otherLink, fieldList, fieldLink }, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
    }

    [Fact]
    public async Task Employee_requester_cannot_link_or_unlink()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var article = await ArticleApiTests.PublishedAsync(world.Desk);
        using var linked = await LinkAsync(world.Desk, ticket, IdOf(article));

        using var link = await LinkAsync(world.Employee, ticket, IdOf(article));
        using var unlink = await world.Employee.DeleteAsync(TicketWorld.PathOf(ticket, $"{ArticlesSegment}/{IdOf(article)}"));

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (link.StatusCode, unlink.StatusCode));
    }

    [Fact]
    public async Task Duplicate_link_is_409_and_unknown_article_is_404()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var article = await ArticleApiTests.PublishedAsync(world.Desk);
        using var first = await LinkAsync(world.Desk, ticket, IdOf(article));

        using var duplicate = await LinkAsync(world.Desk, ticket, IdOf(article));
        using var unknown = await LinkAsync(world.Desk, ticket, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(ErrorCodes.KnowledgeArticleAlreadyLinked, await ApiClient.ReadCodeAsync(duplicate));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Archived_article_cannot_be_linked()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var article = await ArticleApiTests.PublishedAsync(world.Desk);
        using var archive = await world.Desk.PostAsync($"{ArticleApiTests.ArticlesPath}/{IdOf(article)}/archive", new { version = article.GetProperty("version").GetUInt32() });
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);

        using var link = await LinkAsync(world.Desk, ticket, IdOf(article));

        Assert.Equal(ErrorCodes.KnowledgeInvalidTransition, await ApiClient.ReadCodeAsync(link));
    }

    [Fact]
    public async Task Public_reply_with_an_internal_article_link_is_refused_and_writes_nothing()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var internalArticle = await ArticleApiTests.PublishedAsync(world.Desk, "Yalnızca BT", visibility: Internal);
        var draft = await ArticleApiTests.CreateAsync(world.Desk, "Taslak");

        using var viaInternal = await ReplyAsync(world.Desk, ticket, $"Bakın https://itsm.example.com/kb/{IdOf(internalArticle)}", Public);
        using var viaDraft = await ReplyAsync(world.Desk, ticket, $"/kb/{IdOf(draft)}", Public);
        using var viaUnknown = await ReplyAsync(world.Desk, ticket, $"/kb/{Guid.NewGuid()}", Public);

        Assert.All(new[] { viaInternal, viaDraft, viaUnknown }, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(ErrorCodes.KnowledgeInternalArticleInPublicReply, await ApiClient.ReadCodeAsync(viaInternal));
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(0, await db.TicketComments.CountAsync(Ct));
        Assert.Equal(0, await db.OutboxEvents.CountAsync(e => e.EventType == Tgysm.Domain.Notifications.NotificationEventTypes.PublicReply, Ct));
        Assert.Equal(0, await db.TicketEvents.CountAsync(e => e.Kind == Tgysm.Domain.Tickets.TicketEventKind.PublicComment, Ct));
    }

    [Fact]
    public async Task Internal_note_may_link_internal_article_and_public_reply_may_link_employee_article()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        var internalArticle = await ArticleApiTests.PublishedAsync(world.Desk, "Yalnızca BT", visibility: Internal);
        var employeeArticle = await ArticleApiTests.PublishedAsync(world.Desk, "Herkese açık");

        using var note = await ReplyAsync(world.Desk, ticket, $"/kb/{IdOf(internalArticle)}", Internal);
        using var reply = await ReplyAsync(world.Desk, ticket, $"Şunu deneyin: /kb/{IdOf(employeeArticle).ToString().ToUpperInvariant()}", Public);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (note.StatusCode, reply.StatusCode));
    }
}
