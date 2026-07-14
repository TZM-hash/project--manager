namespace ProjectManager.Web.Models;

public sealed class ProjectCollaborationAttachment
{
    public int Id { get; set; }

    public int ProjectCollaborationRecordId { get; set; }

    public ProjectCollaborationRecord? Record { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string RelativePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long Length { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public ApplicationUser? CreatedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
