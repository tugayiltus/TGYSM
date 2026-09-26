using Tgysm.Application.Common.Paging;

namespace Tgysm.Application.Tests;

public sealed class PageRequestTests
{
    [Theory]
    [InlineData(0, 0, PageRequest.FirstPage, 1)]
    [InlineData(-5, 20, PageRequest.FirstPage, 20)]
    [InlineData(3, 1000, 3, PageRequest.MaxPageSize)]
    public void Normalizes_page_and_caps_page_size(int page, int size, int expectedPage, int expectedSize)
    {
        var request = new PageRequest(page, size);

        Assert.Equal(expectedPage, request.NormalizedPage);
        Assert.Equal(expectedSize, request.NormalizedPageSize);
    }
}
