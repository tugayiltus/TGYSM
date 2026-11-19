using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Changes;
using Tgysm.Application.Features.Tasks;
using Tgysm.Application.Features.Tickets;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Common;
using Tgysm.Domain.Knowledge;
using Tgysm.Domain.Tasks;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Search;

public enum SearchHitType
{
    Ticket,
    Change,
    Task,
    Asset,
    Article,
}

/// <summary><paramref name="Key"/> is the ticket, change or task number, the asset tag, or the article id the knowledge base route uses.</summary>
public sealed record SearchHit(SearchHitType Type, string Key, string Title, string? Status);

public sealed record SearchGroup(SearchHitType Type, IReadOnlyList<SearchHit> Hits, bool HasMore);

public sealed record SearchResult(SearchHit? Exact, IReadOnlyList<SearchGroup> Groups);

public static class SearchLimits
{
    public const int MinLength = 2;
    public const int MaxLength = 100;
    public const int PerType = 5;
}

public sealed record SearchQuery(string? Text);

/// <summary>
/// Searches every record type the viewer may see: a full number or tag of a visible record becomes the exact hit,
/// the rest is a text search per type. Visibility of the exact hit equals visibility of that type's text search.
/// </summary>
public sealed class SearchHandler(IAppDbContext db, TicketScope ticketScope, ChangeScope changeScope, TaskScope taskScope)
{
    private const string QueryField = "q";
    private const string QueryInvalidMessage = "The search text must be 2 to 100 characters.";
    private const string Separator = " ";

    private sealed record Row(string Key, string Title, string Status);

    public async Task<SearchResult> HandleAsync(SearchQuery query, CancellationToken cancellationToken)
    {
        var text = query.Text?.Trim() ?? string.Empty;
        if (text.Length is < SearchLimits.MinLength or > SearchLimits.MaxLength)
        {
            throw new ValidationException([new ValidationFailure(QueryField, QueryInvalidMessage) { ErrorCode = ErrorCodes.SearchQueryInvalid }]);
        }

        var viewer = await ticketScope.ViewerAsync(cancellationToken);
        var key = TurkishText.ToSearchKey(text);
        var pattern = LikePattern.Containing(key);

        var tickets = ticketScope.Visible(viewer).AsNoTracking();
        var articles = db.KnowledgeArticles.AsNoTracking().Where(ArticleAccess.ReadableBy(viewer.Role));
        var groups = new List<SearchGroup>();
        SearchHit? exact = null;

        var ticketNumber = TicketNumber.Normalize(text);
        if (TicketNumber.IsValid(ticketNumber))
        {
            exact = await FirstAsync(
                SearchHitType.Ticket,
                tickets.Where(t => t.Number == ticketNumber).Select(t => new Row(t.Number, t.Subject, t.Status.ToString())),
                cancellationToken);
        }

        AddGroup(groups, SearchHitType.Ticket, await TopAsync(
            tickets.Where(t => EF.Functions.Like(t.SearchText, pattern, LikePattern.EscapeText))
                .OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.Id)
                .Select(t => new Row(t.Number, t.Subject, t.Status.ToString())),
            cancellationToken));

        if (viewer.SeesInternal)
        {
            var staff = new ChangeViewer(viewer.UserId, viewer.Role, viewer.TeamIds);
            var changes = changeScope.Visible(staff).AsNoTracking();
            var changeNumber = ChangeNumber.Normalize(text);
            if (exact is null && ChangeNumber.IsValid(changeNumber))
            {
                exact = await FirstAsync(
                    SearchHitType.Change,
                    changes.Where(c => c.Number == changeNumber).Select(c => new Row(c.Number, c.Title, c.Status.ToString())),
                    cancellationToken);
            }

            AddGroup(groups, SearchHitType.Change, await TopAsync(
                changes.Where(c => EF.Functions.Like(c.SearchText, pattern, LikePattern.EscapeText))
                    .OrderByDescending(c => c.UpdatedAt).ThenByDescending(c => c.Id)
                    .Select(c => new Row(c.Number, c.Title, c.Status.ToString())),
                cancellationToken));

            var tasks = taskScope.Visible(new TaskViewer(viewer.UserId, viewer.Role, viewer.TeamIds)).AsNoTracking();
            var taskNumber = TaskNumber.Normalize(text);
            if (exact is null && TaskNumber.IsValid(taskNumber))
            {
                exact = await FirstAsync(
                    SearchHitType.Task,
                    tasks.Where(t => t.Number == taskNumber).Select(t => new Row(t.Number, t.Title, t.Status.ToString())),
                    cancellationToken);
            }

            AddGroup(groups, SearchHitType.Task, await TopAsync(
                tasks.Where(t => EF.Functions.Like(t.SearchText, pattern, LikePattern.EscapeText))
                    .OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.Id)
                    .Select(t => new Row(t.Number, t.Title, t.Status.ToString())),
                cancellationToken));

            var assets = db.Assets.AsNoTracking();
            var code = Asset.NormalizeTag(text);
            var codePattern = LikePattern.Containing(code);
            if (exact is null)
            {
                exact = await FirstAsync(
                    SearchHitType.Asset,
                    assets.Where(a => a.NormalizedAssetTag == code).Select(a => new Row(a.AssetTag, a.Brand + Separator + a.Model, a.Status.ToString())),
                    cancellationToken);
            }

            AddGroup(groups, SearchHitType.Asset, await TopAsync(
                assets.Where(a => EF.Functions.Like(a.NormalizedAssetTag, codePattern, LikePattern.EscapeText)
                        || (a.NormalizedSerialNumber != null && EF.Functions.Like(a.NormalizedSerialNumber, codePattern, LikePattern.EscapeText)))
                    .OrderByDescending(a => a.UpdatedAt).ThenByDescending(a => a.Id)
                    .Select(a => new Row(a.AssetTag, a.Brand + Separator + a.Model, a.Status.ToString())),
                cancellationToken));
        }

        AddGroup(groups, SearchHitType.Article, await TopAsync(
            articles.Where(a => EF.Functions.Like(a.SearchText, pattern, LikePattern.EscapeText))
                .OrderByDescending(a => a.UpdatedAt).ThenByDescending(a => a.Id)
                .Select(a => new Row(a.Id.ToString(), a.Title, a.Status.ToString())),
            cancellationToken));

        return new SearchResult(exact, [.. groups.OrderBy(g => g.Type)]);
    }

    private static string StatusName(string status) => JsonNamingPolicy.CamelCase.ConvertName(status);

    private static async Task<SearchHit?> FirstAsync(SearchHitType type, IQueryable<Row> rows, CancellationToken cancellationToken)
    {
        var row = await rows.FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : new SearchHit(type, row.Key, row.Title, StatusName(row.Status));
    }

    private static async Task<List<Row>> TopAsync(IQueryable<Row> rows, CancellationToken cancellationToken) =>
        await rows.Take(SearchLimits.PerType + 1).ToListAsync(cancellationToken);

    private static void AddGroup(List<SearchGroup> groups, SearchHitType type, List<Row> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var hits = rows.Take(SearchLimits.PerType).Select(r => new SearchHit(type, r.Key, r.Title, StatusName(r.Status))).ToList();
        groups.Add(new SearchGroup(type, hits, rows.Count > SearchLimits.PerType));
    }
}
