using Microsoft.EntityFrameworkCore;

namespace ProjectManager.Web.Services;

/// <summary>
/// 列表页统一允许的分页条数。所有入口都走这里，避免页面和服务层出现不一致默认值。
/// </summary>
public static class PageSizeOptions
{
    public const int DefaultPageSize = 20;

    public static readonly int[] Allowed = [10, 20, 50, 100];

    public static int NormalizePageSize(int pageSize)
    {
        return Allowed.Contains(pageSize) ? pageSize : DefaultPageSize;
    }

    public static int NormalizePageNumber(int pageNumber)
    {
        return pageNumber < 1 ? 1 : pageNumber;
    }
}

/// <summary>
/// 数据库分页结果。创建时会先 Count 再 Skip/Take，确保列表不会先加载全量数据再内存分页。
/// </summary>
public sealed class PagedResult<T>
{
    public PagedResult(
        IReadOnlyList<T> items,
        int totalCount,
        int pageNumber,
        int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        PageSize = PageSizeOptions.NormalizePageSize(pageSize);
        TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        PageNumber = Math.Min(PageSizeOptions.NormalizePageNumber(pageNumber), TotalPages);
    }

    public IReadOnlyList<T> Items { get; }

    public int TotalCount { get; }

    public int PageNumber { get; }

    public int PageSize { get; }

    public int TotalPages { get; }

    public static async Task<PagedResult<T>> CreateAsync(
        IQueryable<T> query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedPageSize = PageSizeOptions.NormalizePageSize(pageSize);
        var normalizedPageNumber = PageSizeOptions.NormalizePageNumber(pageNumber);
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)normalizedPageSize));
        normalizedPageNumber = Math.Min(normalizedPageNumber, totalPages);

        var items = await query
            .Skip((normalizedPageNumber - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalCount, normalizedPageNumber, normalizedPageSize);
    }
}
