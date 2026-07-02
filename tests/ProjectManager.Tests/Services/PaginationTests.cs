using FluentAssertions;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class PaginationTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public void NormalizePageSize_accepts_supported_page_sizes(int pageSize)
    {
        PageSizeOptions.NormalizePageSize(pageSize).Should().Be(pageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(500)]
    public void NormalizePageSize_falls_back_to_default_for_unsupported_values(int pageSize)
    {
        PageSizeOptions.NormalizePageSize(pageSize).Should().Be(20);
    }

    [Theory]
    [InlineData(-3)]
    [InlineData(0)]
    public void NormalizePageNumber_falls_back_to_first_page(int pageNumber)
    {
        PageSizeOptions.NormalizePageNumber(pageNumber).Should().Be(1);
    }
}
