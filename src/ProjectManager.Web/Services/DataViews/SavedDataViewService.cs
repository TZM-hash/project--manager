using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services.DataViews;

public sealed class SavedDataViewService(
    ApplicationDbContext db,
    DataViewRegistry registry,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<SavedDataViewSnapshot>> ListAsync(
        string userId,
        string pageKey,
        CancellationToken cancellationToken)
    {
        var definition = registry.Get(pageKey);
        var views = await db.SavedDataViews
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.PageKey == definition.PageKey)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return views.Select(x => ToSnapshot(x, definition)).ToArray();
    }

    public async Task<SavedDataViewSnapshot> SaveAsync(
        string userId,
        SaveDataViewCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var definition = registry.Get(command.PageKey);
        var name = NormalizeName(command.Name);
        var filters = NormalizeFilters(command.Filters, definition);
        var columns = NormalizeColumns(command.VisibleColumns, definition);
        var now = timeProvider.GetUtcNow();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (command.IsDefault)
        {
            await db.SavedDataViews
                .Where(x => x.UserId == userId && x.PageKey == definition.PageKey && x.IsDefault)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.IsDefault, false)
                        .SetProperty(x => x.UpdatedAt, now),
                    cancellationToken);
        }

        var entity = await db.SavedDataViews.SingleOrDefaultAsync(
            x => x.UserId == userId && x.PageKey == definition.PageKey && x.Name == name,
            cancellationToken);
        if (entity is null)
        {
            entity = new SavedDataView
            {
                UserId = userId,
                PageKey = definition.PageKey,
                Name = name,
                CreatedAt = now
            };
            db.SavedDataViews.Add(entity);
        }

        entity.FilterJson = Serialize(filters);
        entity.ColumnJson = Serialize(columns);
        entity.RowDensity = Enum.IsDefined(command.RowDensity) ? command.RowDensity : DataViewRowDensity.Normal;
        entity.IsDefault = command.IsDefault;
        entity.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToSnapshot(entity, definition);
    }

    public async Task<bool> DeleteAsync(string userId, int id, CancellationToken cancellationToken)
    {
        var entity = await db.SavedDataViews.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userId,
            cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.SavedDataViews.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetDefaultAsync(string userId, int id, CancellationToken cancellationToken)
    {
        var entity = await db.SavedDataViews.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userId,
            cancellationToken);
        if (entity is null)
        {
            return false;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.SavedDataViews
            .Where(x => x.UserId == userId && x.PageKey == entity.PageKey && x.IsDefault)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsDefault, false), cancellationToken);
        entity.IsDefault = true;
        entity.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static SavedDataViewSnapshot ToSnapshot(SavedDataView entity, DataViewDefinition definition)
    {
        return new(
            entity.Id,
            entity.PageKey,
            entity.Name,
            NormalizeFilters(DeserializeDictionary(entity.FilterJson), definition),
            NormalizeColumns(DeserializeColumns(entity.ColumnJson), definition),
            Enum.IsDefined(entity.RowDensity) ? entity.RowDensity : DataViewRowDensity.Normal,
            entity.IsDefault);
    }

    private static Dictionary<string, string?> NormalizeFilters(
        IReadOnlyDictionary<string, string?>? filters,
        DataViewDefinition definition)
    {
        if (filters is null)
        {
            return [];
        }

        return filters
            .Where(x => definition.FilterKeys.Contains(x.Key))
            .ToDictionary(x => x.Key, x => x.Value?.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static string[] NormalizeColumns(
        IReadOnlyList<string>? columns,
        DataViewDefinition definition)
    {
        var valid = definition.Columns.Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalized = (columns ?? [])
            .Where(valid.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var fixedColumn in definition.Columns.Where(x => x.Fixed).Select(x => x.Key))
        {
            if (!normalized.Contains(fixedColumn, StringComparer.OrdinalIgnoreCase))
            {
                normalized.Add(fixedColumn);
            }
        }

        return normalized.Count > 0
            ? normalized.ToArray()
            : definition.Columns.Where(x => x.DefaultVisible).Select(x => x.Key).ToArray();
    }

    private static Dictionary<string, string?> DeserializeDictionary(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string[] DeserializeColumns(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Serialize<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        if (json.Length > 8000)
        {
            throw new ArgumentException("資料檢視設定超過允許長度。", nameof(value));
        }

        return json;
    }

    private static string NormalizeName(string name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 80)
        {
            throw new ArgumentException("檢視名稱長度必須為 1 到 80 個字。", nameof(name));
        }

        return normalized;
    }
}
