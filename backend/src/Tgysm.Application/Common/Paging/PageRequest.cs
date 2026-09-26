namespace Tgysm.Application.Common.Paging;

public sealed record PageRequest(int Page, int PageSize)
{
    public const int FirstPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    private const int MinPageSize = 1;

    public int NormalizedPage => Math.Max(FirstPage, Page);

    public int NormalizedPageSize => Math.Clamp(PageSize, MinPageSize, MaxPageSize);
}
