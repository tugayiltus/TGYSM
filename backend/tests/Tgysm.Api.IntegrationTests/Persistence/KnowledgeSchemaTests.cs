using Microsoft.EntityFrameworkCore;
using Npgsql;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Common;
using Tgysm.Domain.Knowledge;
using Tgysm.Domain.Organization;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class KnowledgeSchemaTests(PostgresDatabase database) : IAsyncLifetime
{
    private const string UniqueViolation = "23505";
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private User _user = null!;
    private Ticket _ticket = null!;
    private KnowledgeArticle _article = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetAsync();
        _user = User.Create("Ayşe", "Kaya", "ayse@example.com", UserRole.ItStaff, null, null, "hash", Now);
        var team = Team.Create("Servis Masası");
        _ticket = Ticket.CreateIncident("INC-2026-000001", "Yazıcı", "Çalışmıyor", null, null, _user.Id, team.Id, Now);
        _article = KnowledgeArticle.Create("VPN", "Özet", "Gövde", null, null, ArticleVisibility.Employees, _user.Id, Now);
        await using var db = database.CreateDbContext();
        db.Users.Add(_user);
        db.Teams.Add(team);
        db.Tickets.Add(_ticket);
        db.KnowledgeArticles.Add(_article);
        db.ArticleRevisions.Add(ArticleRevision.From(_article, Now));
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Duplicate_ticket_article_pair_is_rejected_by_the_database()
    {
        await using var db = database.CreateDbContext();
        db.TicketArticles.Add(TicketArticle.Create(_ticket.Id, _article.Id, _user.Id, Now));
        await db.SaveChangesAsync(Ct);

        await using var second = database.CreateDbContext();
        second.TicketArticles.Add(TicketArticle.Create(_ticket.Id, _article.Id, _user.Id, Now));
        var ex = await Assert.ThrowsAsync<DomainException>(() => second.SaveChangesAsync(Ct));

        Assert.Equal(UniqueViolation, Assert.IsType<PostgresException>(ex.InnerException!.InnerException).SqlState);
    }

    [Fact]
    public async Task Duplicate_revision_number_is_rejected_by_the_database()
    {
        await using var db = database.CreateDbContext();
        db.ArticleRevisions.Add(ArticleRevision.From(_article, Now));

        var ex = await Assert.ThrowsAsync<DomainException>(() => db.SaveChangesAsync(Ct));

        Assert.Equal(UniqueViolation, Assert.IsType<PostgresException>(ex.InnerException!.InnerException).SqlState);
    }

    [Fact]
    public async Task Article_round_trips_with_snake_case_enums_and_search_index()
    {
        await using var db = database.CreateDbContext();
        var status = await db.Database.SqlQuery<string>($"SELECT status AS \"Value\" FROM knowledge_articles").SingleAsync(Ct);
        var index = await db.Database.SqlQuery<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE indexname = 'ix_knowledge_articles_search_text_trgm'").SingleAsync(Ct);

        Assert.Equal("draft", status);
        Assert.Equal("ix_knowledge_articles_search_text_trgm", index);
    }
}
