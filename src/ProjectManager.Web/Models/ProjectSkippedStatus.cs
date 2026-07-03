namespace ProjectManager.Web.Models;

/// <summary>
/// Project-specific status node that should be hidden from timeline display.
/// </summary>
public sealed class ProjectSkippedStatus
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    public int StatusId { get; set; }

    public ProjectStatus? Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? CreatedByUserId { get; set; }

    public ApplicationUser? CreatedByUser { get; set; }
}
