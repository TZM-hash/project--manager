namespace ProjectManager.Web.Models;

public sealed class SystemSetting
{
    public int Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum UiEffectsLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}
