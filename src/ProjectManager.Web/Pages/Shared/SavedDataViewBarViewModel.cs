using ProjectManager.Web.Models;
using ProjectManager.Web.Services.DataViews;

namespace ProjectManager.Web.Pages.Shared;

public sealed record SavedDataViewBarViewModel(
    string PageKey,
    IReadOnlyList<DataViewPreset> SystemPresets,
    IReadOnlyList<SavedDataViewSnapshot> PersonalViews,
    string? SelectedPresetKey,
    int? SelectedViewId,
    IReadOnlyDictionary<string, string?> CurrentFilters,
    IReadOnlyList<string> VisibleColumns,
    DataViewRowDensity RowDensity,
    string ReturnUrl)
{
    public SavedDataViewSnapshot? SelectedPersonalView =>
        PersonalViews.FirstOrDefault(view => view.Id == SelectedViewId);
}

public sealed class SaveDataViewInput
{
    public string Name { get; set; } = string.Empty;

    public string FilterJson { get; set; } = "{}";

    public string ColumnJson { get; set; } = "[]";

    public DataViewRowDensity RowDensity { get; set; } = DataViewRowDensity.Normal;

    public bool IsDefault { get; set; }

    public string ReturnUrl { get; set; } = string.Empty;
}

public sealed record ResolvedDataView(
    SavedDataViewBarViewModel Bar,
    IReadOnlyDictionary<string, string?> Filters,
    IReadOnlyList<string> VisibleColumns,
    DataViewRowDensity RowDensity);
