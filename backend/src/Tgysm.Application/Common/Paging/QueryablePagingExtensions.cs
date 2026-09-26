using Microsoft.EntityFrameworkCore;

namespace Tgysm.Application.Common.Paging;

public static class QueryablePagingExtensions
{
    /// <summary>The query must already be ordered; paging an unordered query returns unstable pages.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PageRequest request, CancellationToken cancellationToken)
    {
        var page = request.NormalizedPage;
        var size = request.NormalizedPageSize;
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - PageRequest.FirstPage) * size).Take(size).ToListAsync(cancellationToken);
        return new PagedResult<T>(items, page, size, total);
    }
}
