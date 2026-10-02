using Tgysm.Domain.Knowledge;

namespace Tgysm.Api.Contracts;

public sealed record CreateArticleRequest(string Title, string Summary, string Body, Guid? CategoryId, ArticleVisibility Visibility);

public sealed record UpdateArticleRequest(string Title, string Summary, string Body, Guid? CategoryId, ArticleVisibility Visibility, uint Version);
