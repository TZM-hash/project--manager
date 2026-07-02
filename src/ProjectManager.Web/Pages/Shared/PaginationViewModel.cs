using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Shared;

public sealed class PaginationViewModel
{
    public PaginationViewModel(
        int pageNumber,
        int pageSize,
        int totalCount,
        int totalPages,
        string pageName,
        IReadOnlyDictionary<string, string?> routeValues)
    {
        PageNumber = ProjectManager.Web.Services.PageSizeOptions.NormalizePageNumber(pageNumber);
        PageSize = ProjectManager.Web.Services.PageSizeOptions.NormalizePageSize(pageSize);
        TotalCount = totalCount;
        TotalPages = Math.Max(1, totalPages);
        PageName = pageName;
        RouteValues = routeValues;
    }

    public int PageNumber { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages { get; }

    public string PageName { get; }

    public IReadOnlyDictionary<string, string?> RouteValues { get; }

    public IReadOnlyList<int> PageSizeOptions => ProjectManager.Web.Services.PageSizeOptions.Allowed;

    public Dictionary<string, string> RouteFor(int pageNumber, int? pageSize = null)
    {
        var route = RouteValues
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .ToDictionary(x => x.Key, x => x.Value!);
        route["PageNumber"] = ProjectManager.Web.Services.PageSizeOptions.NormalizePageNumber(pageNumber).ToString();
        route["PageSize"] = ProjectManager.Web.Services.PageSizeOptions.NormalizePageSize(pageSize ?? PageSize).ToString();
        return route;
    }

    public Dictionary<string, string> RouteWithoutPaging()
    {
        return RouteValues
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .ToDictionary(x => x.Key, x => x.Value!);
    }
}
