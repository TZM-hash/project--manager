namespace ProjectManager.Web.Pages.Shared;

public sealed record FilterSummaryItem(string Label, string Value);

public sealed class FilterSummaryViewModel
{
    public FilterSummaryViewModel(
        string pageName,
        IReadOnlyList<FilterSummaryItem> items,
        Dictionary<string, string?>? clearRouteValues = null)
    {
        PageName = pageName;
        Items = items;
        ClearRouteValues = clearRouteValues ?? [];
    }

    public string PageName { get; }

    public IReadOnlyList<FilterSummaryItem> Items { get; }

    public Dictionary<string, string?> ClearRouteValues { get; }
}
