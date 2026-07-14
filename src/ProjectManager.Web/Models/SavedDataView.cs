namespace ProjectManager.Web.Models;

public enum DataViewRowDensity
{
    Compact = 1,
    Normal = 2,
    Spacious = 3
}

public sealed class SavedDataView
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser? User { get; set; }

    public string PageKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string FilterJson { get; set; } = "{}";

    public string ColumnJson { get; set; } = "{}";

    public DataViewRowDensity RowDensity { get; set; } = DataViewRowDensity.Normal;

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
