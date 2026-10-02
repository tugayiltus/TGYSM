using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Knowledge;

namespace Tgysm.Api.IntegrationTests.Knowledge;

[Collection(ApiCollection.Name)]
public sealed class ArticleApiTests(ApiFactory factory) : IAsyncLifetime
{
    public const string ArticlesPath = "/api/v1/kb/articles";
    private const int RaceRounds = 6;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public static async Task<JsonElement> CreateAsync(
        ApiClient client, string title = "Yazıcı kurulumu", string body = "Adım adım kurulum", string visibility = "employees", Guid? categoryId = null)
    {
        using var response = await client.PostAsync(ArticlesPath, new { title, summary = "Kısa özet", body, categoryId, visibility });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    public static async Task<JsonElement> PublishedAsync(ApiClient client, string title = "Yazıcı kurulumu", string body = "Adım adım kurulum", string visibility = "employees")
    {
        var created = await CreateAsync(client, title, body, visibility);
        using var response = await client.PostAsync($"{PathOf(created)}/publish", new { version = VersionOf(created) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private static string PathOf(JsonElement article) => $"{ArticlesPath}/{article.GetProperty("id").GetGuid()}";

    private static uint VersionOf(JsonElement article) => article.GetProperty("version").GetUInt32();

    private static async Task<string[]> TitlesAsync(ApiClient client, string query)
    {
        var page = await TicketWorld.ReadAsync(client, $"{ArticlesPath}{query}");
        return [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()!)];
    }

    private static object UpdateBody(JsonElement article, string title = "Yeni başlık") =>
        new { title, summary = "Kısa özet", body = "Yeni gövde", categoryId = (Guid?)null, visibility = "employees", version = VersionOf(article) };

    [Fact]
    public async Task IT_create_makes_draft_with_revision_one_and_audit()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var article = await CreateAsync(world.Desk);
        Assert.Equal("draft", article.GetProperty("status").GetString());

        await using var db = factory.Database.CreateDbContext();
        var id = article.GetProperty("id").GetGuid();
        Assert.Equal(1, await db.ArticleRevisions.CountAsync(r => r.ArticleId == id, Ct));
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.KnowledgeCreated && a.EntityId == id, Ct));
    }

    [Fact]
    public async Task Update_with_stale_version_is_409_and_adds_no_revision()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var article = await CreateAsync(world.Desk);
        using var first = await world.Desk.PutAsync(PathOf(article), UpdateBody(article));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var stale = await world.Desk.PutAsync(PathOf(article), UpdateBody(article, "Eski sürümden"));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(2, await db.ArticleRevisions.CountAsync(r => r.ArticleId == article.GetProperty("id").GetGuid(), Ct));
    }

    [Fact]
    public async Task Two_parallel_updates_with_same_version_give_one_200_and_one_409()
    {
        var world = await TicketWorld.CreateAsync(factory);
        for (var round = 0; round < RaceRounds; round++)
        {
            var article = await CreateAsync(world.Desk, title: $"Yarış {round}");
            var responses = await Task.WhenAll(
                world.Desk.PutAsync(PathOf(article), UpdateBody(article, "A")),
                world.Admin.PutAsync(PathOf(article), UpdateBody(article, "B")));
            var codes = responses.Select(r => r.StatusCode).Order().ToArray();
            foreach (var response in responses)
            {
                response.Dispose();
            }

            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], codes);
        }
    }

    [Fact]
    public async Task Published_article_is_visible_to_employee_in_list_and_by_id()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var draft = await CreateAsync(world.Desk, "Taslak makale");
        Assert.DoesNotContain("Taslak makale", await TitlesAsync(world.Employee, string.Empty));

        var published = await PublishedAsync(world.Desk, "Yayında makale");

        Assert.Contains("Yayında makale", await TitlesAsync(world.Employee, string.Empty));
        using var byId = await world.Employee.GetAsync(PathOf(published));
        Assert.Equal(HttpStatusCode.OK, byId.StatusCode);
        using var draftById = await world.Employee.GetAsync(PathOf(draft));
        Assert.Equal(HttpStatusCode.NotFound, draftById.StatusCode);
    }

    [Fact]
    public async Task Employee_gets_identical_404_for_draft_internal_archived_and_unknown()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var draft = await CreateAsync(world.Desk);
        var internalArticle = await PublishedAsync(world.Desk, "İç makale", visibility: "internal");
        var archived = await PublishedAsync(world.Desk, "Arşiv makale");
        using var archive = await world.Desk.PostAsync($"{PathOf(archived)}/archive", new { version = VersionOf(archived) });
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);

        var bodies = new List<string>();
        foreach (var url in new[] { PathOf(draft), PathOf(internalArticle), PathOf(archived), $"{ArticlesPath}/{Guid.NewGuid()}" })
        {
            using var response = await world.Employee.GetAsync(url);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            bodies.Add(await response.Content.ReadAsStringAsync(Ct));
        }

        Assert.Single(bodies.Select(b => System.Text.RegularExpressions.Regex.Replace(b, "\"(traceId|correlationId)\":\"[^\"]*\"", string.Empty)).Distinct());
    }

    [Fact]
    public async Task Employee_filters_are_ignored_and_only_published_employee_articles_return()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await CreateAsync(world.Desk, "Taslak");
        await PublishedAsync(world.Desk, "İç yayın", visibility: "internal");
        await PublishedAsync(world.Desk, "Herkese açık");

        Assert.Equal(["Herkese açık"], await TitlesAsync(world.Employee, "?status=draft&visibility=internal"));
        Assert.Equal(3, (await TitlesAsync(world.Desk, string.Empty)).Length);
        Assert.Equal(["Taslak"], await TitlesAsync(world.Desk, "?status=draft"));
    }

    [Fact]
    public async Task Search_folds_turkish_letters_and_ranks_title_matches_first()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await PublishedAsync(world.Desk, "IŞIK Ayarları");
        await PublishedAsync(world.Desk, "Isik Raporu");
        await PublishedAsync(world.Desk, "Başka konu", body: "içinde ışık geçen gövde");

        Assert.Contains("IŞIK Ayarları", await TitlesAsync(world.Employee, "?search=ışık"));
        Assert.Contains("IŞIK Ayarları", await TitlesAsync(world.Employee, "?search=IŞIK"));
        Assert.Contains("Isik Raporu", await TitlesAsync(world.Employee, "?search=Isik"));
        var ranked = await TitlesAsync(world.Employee, "?search=ışık");
        Assert.Equal("IŞIK Ayarları", ranked[0]);
        Assert.Equal("Başka konu", ranked[^1]);
    }

    [Fact]
    public async Task Search_matching_only_internal_article_body_is_hidden_from_employee()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await PublishedAsync(world.Desk, "İç rehber", body: "gizlikelime burada", visibility: "internal");

        Assert.Empty(await TitlesAsync(world.Employee, "?search=gizlikelime"));
        Assert.Equal(["İç rehber"], await TitlesAsync(world.Desk, "?search=gizlikelime"));
    }

    [Fact]
    public async Task Punctuation_only_search_returns_the_normal_list()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await PublishedAsync(world.Desk, "Birinci");
        await PublishedAsync(world.Desk, "İkinci");

        Assert.Equal(2, (await TitlesAsync(world.Employee, "?search=%20%20%2C.%3B%20%20")).Length);
    }

    [Fact]
    public async Task Employee_cannot_write_or_publish()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var article = await PublishedAsync(world.Desk);

        using var post = await world.Employee.PostAsync(ArticlesPath, new { title = "x", summary = "y", body = "z", visibility = "employees" });
        using var put = await world.Employee.PutAsync(PathOf(article), UpdateBody(article));
        using var publish = await world.Employee.PostAsync($"{PathOf(article)}/publish", new { version = VersionOf(article) });
        using var archive = await world.Employee.PostAsync($"{PathOf(article)}/archive", new { version = VersionOf(article) });
        using var restore = await world.Employee.PostAsync($"{PathOf(article)}/restore", new { version = VersionOf(article) });

        Assert.All([post, put, publish, archive, restore], r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
    }

    [Fact]
    public async Task Restore_from_archived_gives_draft_hidden_from_employees()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var article = await PublishedAsync(world.Desk, "Döngü");
        using var archive = await world.Desk.PostAsync($"{PathOf(article)}/archive", new { version = VersionOf(article) });
        var archived = await world.Desk.ReadJsonAsync<JsonElement>(archive);
        Assert.Empty(await TitlesAsync(world.Employee, string.Empty));

        using var restore = await world.Desk.PostAsync($"{PathOf(archived)}/restore", new { version = VersionOf(archived) });

        var restored = await world.Desk.ReadJsonAsync<JsonElement>(restore);
        Assert.Equal("draft", restored.GetProperty("status").GetString());
        Assert.Empty(await TitlesAsync(world.Employee, string.Empty));
    }

    [Fact]
    public async Task Revisions_are_listed_for_IT_and_forbidden_for_employee()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var article = await CreateAsync(world.Desk);
        using var update = await world.Desk.PutAsync(PathOf(article), UpdateBody(article));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var revisions = await TicketWorld.ReadAsync(world.Desk, $"{PathOf(article)}/revisions");
        using var forbidden = await world.Employee.GetAsync($"{PathOf(article)}/revisions");

        Assert.Equal([2, 1], revisions.EnumerateArray().Select(r => r.GetProperty("revision").GetInt32()).ToArray());
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Visibility_change_writes_extra_audit_row()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var article = await CreateAsync(world.Desk);
        using var update = await world.Desk.PutAsync(PathOf(article), new { title = "T", summary = "S", body = "B", categoryId = (Guid?)null, visibility = "internal", version = VersionOf(article) });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        await using var db = factory.Database.CreateDbContext();
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.KnowledgeVisibilityChanged, Ct));
    }

    [Fact]
    public async Task Invalid_input_gives_field_errors()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var inactive = await TestData.CategoryAsync(factory, "Pasif", active: false);

        using var blank = await world.Desk.PostAsync(ArticlesPath, new { title = " ", summary = "", body = "", visibility = "employees" });
        using var tooLong = await world.Desk.PostAsync(ArticlesPath, new { title = new string('a', ArticleLimits.TitleMaxLength + 1), summary = "s", body = "b", visibility = "employees" });
        using var unknownCategory = await world.Desk.PostAsync(ArticlesPath, new { title = "t", summary = "s", body = "b", categoryId = Guid.NewGuid(), visibility = "employees" });
        using var inactiveCategory = await world.Desk.PostAsync(ArticlesPath, new { title = "t", summary = "s", body = "b", categoryId = inactive.Id, visibility = "employees" });

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        var blankFields = await ApiClient.ReadFieldCodesAsync(blank);
        Assert.All(new[] { "title", "summary", "body" }, f => Assert.Contains(f, blankFields.Keys, StringComparer.OrdinalIgnoreCase));
        Assert.Contains("title", (await ApiClient.ReadFieldCodesAsync(tooLong)).Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var response in new[] { unknownCategory, inactiveCategory })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("categoryId", await ApiClient.ReadFieldCodesAsync(response) is var f ? f.Keys : [], StringComparer.OrdinalIgnoreCase);
        }
    }
}
