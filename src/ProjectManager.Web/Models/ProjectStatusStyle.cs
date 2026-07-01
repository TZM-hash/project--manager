namespace ProjectManager.Web.Models;

public sealed class ProjectStatusStyle
{
    public int Id { get; set; }

    public int StatusId { get; set; }

    public ProjectStatus? Status { get; set; }

    public string TextColor { get; set; } = "#1f2937";

    public string BackgroundColor { get; set; } = "#e5e7eb";

    public bool IsBold { get; set; }
}
