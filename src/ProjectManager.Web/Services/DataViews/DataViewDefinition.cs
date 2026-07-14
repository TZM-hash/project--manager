using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services.DataViews;

public sealed record DataViewColumnDefinition(
    string Key,
    string Label,
    bool DefaultVisible = true,
    bool Fixed = false);

public sealed record DataViewPreset(
    string Key,
    string Name,
    IReadOnlyDictionary<string, string?> Filters,
    IReadOnlyList<string> VisibleColumns,
    DataViewRowDensity RowDensity = DataViewRowDensity.Normal);

public sealed record DataViewDefinition(
    string PageKey,
    IReadOnlySet<string> FilterKeys,
    IReadOnlyList<DataViewColumnDefinition> Columns,
    IReadOnlyList<DataViewPreset> Presets);

public sealed record SaveDataViewCommand(
    string PageKey,
    string Name,
    IReadOnlyDictionary<string, string?> Filters,
    IReadOnlyList<string> VisibleColumns,
    DataViewRowDensity RowDensity,
    bool IsDefault);

public sealed record SavedDataViewSnapshot(
    int Id,
    string PageKey,
    string Name,
    IReadOnlyDictionary<string, string?> Filters,
    IReadOnlyList<string> VisibleColumns,
    DataViewRowDensity RowDensity,
    bool IsDefault);
