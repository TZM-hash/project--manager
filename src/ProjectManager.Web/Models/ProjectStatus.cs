namespace ProjectManager.Web.Models;

public sealed class ProjectStatus
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsClosed { get; set; }

    public bool IsActive { get; set; } = true;

    public ProjectStatusStyle? Style { get; set; }
}
