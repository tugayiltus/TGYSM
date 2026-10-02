using Tgysm.Domain.Knowledge;

namespace Tgysm.Application.Features.Knowledge;

public sealed record ArticleListItem(
    Guid Id,
    string Title,
    string Summary,
    Guid? CategoryId,
    string? CategoryName,
    ArticleVisibility Visibility,
    ArticleStatus Status,
    DateTimeOffset UpdatedAt);

public sealed record ArticleDetail(
    Guid Id,
    string Title,
    string Summary,
    string Body,
    Guid? CategoryId,
    string? CategoryName,
    ArticleVisibility Visibility,
    ArticleStatus Status,
    string AuthorName,
    DateTimeOffset? PublishedAt,
    DateTimeOffset UpdatedAt,
    uint Version);

public sealed record ArticleRevisionItem(int Revision, string Title, ArticleVisibility Visibility, string EditedByName, DateTimeOffset EditedAt);
