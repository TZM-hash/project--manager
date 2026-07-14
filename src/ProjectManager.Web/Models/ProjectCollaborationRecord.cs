namespace ProjectManager.Web.Models;

public sealed class ProjectCollaborationRecord
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    public string Category { get; set; } = "進度協作";

    public string Content { get; set; } = string.Empty;

    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser? CreatedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public byte[] RowVersion { get; set; } = [];
}
