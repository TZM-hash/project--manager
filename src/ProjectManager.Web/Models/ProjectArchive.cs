namespace ProjectManager.Web.Models;

public sealed class ProjectArchive
{
    public int Id { get; set; }

    public int OriginalProjectId { get; set; }

    public int Year { get; set; }

    public ProjectType ProjectType { get; set; }

    public string? ParentCaseNumber { get; set; }

    public string ProjectNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal ProgressPercent { get; set; }

    public decimal ProjectAmount { get; set; }

    public decimal CollectionPercent { get; set; }

    public string? ProgressDescription { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public bool StatusIsClosed { get; set; }

    public DateOnly? ClosedYearMonth { get; set; }

    public string? ArchivedByUserId { get; set; }

    public ApplicationUser? ArchivedByUser { get; set; }

    public DateTimeOffset ArchivedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset OriginalCreatedAt { get; set; }

    public DateTimeOffset OriginalUpdatedAt { get; set; }

    public string AssignmentSummary { get; set; } = string.Empty;

    /// <summary>經辦人員使用者 ID 列表（逗號分隔），用於還原時重建指派關係。</summary>
    public string? AssignmentUserIds { get; set; }
}
