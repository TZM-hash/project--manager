using System.Text.Json;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services.DataViews;

namespace ProjectManager.Web.Pages.Shared;

public sealed class SavedDataViewPageSupport(
    SavedDataViewService service,
    DataViewRegistry registry)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ResolvedDataView> ResolveAsync(
        string userId,
        string pageKey,
        string? selectedPresetKey,
        int? selectedViewId,
        IReadOnlyDictionary<string, string?> explicitFilters,
        bool hasExplicitFilters,
        string returnUrl,
        CancellationToken cancellationToken)
    {
        var definition = registry.Get(pageKey);
        var personalViews = await service.ListAsync(userId, pageKey, cancellationToken);
        var selectedPersonal = selectedViewId.HasValue
            ? personalViews.FirstOrDefault(view => view.Id == selectedViewId.Value)
            : null;
        var selectedPreset = !string.IsNullOrWhiteSpace(selectedPresetKey)
            ? definition.Presets.FirstOrDefault(preset => string.Equals(preset.Key, selectedPresetKey, StringComparison.OrdinalIgnoreCase))
            : null;

        if (selectedPersonal is null && selectedPreset is null && !hasExplicitFilters)
        {
            selectedPersonal = personalViews.FirstOrDefault(view => view.IsDefault);
        }

        selectedPreset ??= selectedPersonal is null
            ? definition.Presets.FirstOrDefault()
            : null;

        var filters = hasExplicitFilters
            ? explicitFilters
            : selectedPersonal?.Filters ?? selectedPreset?.Filters ?? explicitFilters;
        var visibleColumns = selectedPersonal?.VisibleColumns
            ?? selectedPreset?.VisibleColumns
            ?? definition.Columns.Where(column => column.DefaultVisible).Select(column => column.Key).ToArray();
        var rowDensity = selectedPersonal?.RowDensity
            ?? selectedPreset?.RowDensity
            ?? DataViewRowDensity.Normal;

        var bar = new SavedDataViewBarViewModel(
            definition.PageKey,
            definition.Presets,
            personalViews,
            selectedPreset?.Key,
            selectedPersonal?.Id,
            filters,
            visibleColumns,
            rowDensity,
            returnUrl);

        return new(bar, filters, visibleColumns, rowDensity);
    }

    public Task<SavedDataViewSnapshot> SaveAsync(
        string userId,
        string pageKey,
        SaveDataViewInput input,
        CancellationToken cancellationToken)
    {
        var filters = Deserialize<Dictionary<string, string?>>(input.FilterJson) ?? [];
        var columns = Deserialize<string[]>(input.ColumnJson) ?? [];
        return service.SaveAsync(
            userId,
            new SaveDataViewCommand(
                pageKey,
                input.Name,
                filters,
                columns,
                input.RowDensity,
                input.IsDefault),
            cancellationToken);
    }

    public Task<bool> DeleteAsync(string userId, int id, CancellationToken cancellationToken) =>
        service.DeleteAsync(userId, id, cancellationToken);

    public Task<bool> SetDefaultAsync(string userId, int id, CancellationToken cancellationToken) =>
        service.SetDefaultAsync(userId, id, cancellationToken);

    private static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
