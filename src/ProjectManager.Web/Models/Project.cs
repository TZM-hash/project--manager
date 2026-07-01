namespace ProjectManager.Web.Models;

public sealed class Project
{
    public int Id { get; set; }

    public int Year { get; set; }

    public string? ParentCaseNumber { get; set; }

    public string ProjectNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal ProgressPercent { get; set; }

    public decimal ProjectAmount { get; set; }

    public decimal CollectionPercent { get; set; }

    public string? ProgressDescription { get; set; }

    public int StatusId { get; set; }

    public ProjectStatus? Status { get; set; }

    public string? UpdatedByUserId { get; set; }

    public ApplicationUser? UpdatedByUser { get; set; }

    public DateOnly? ClosedYearMonth { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsDeleted { get; set; }

    public ICollection<ProjectAssignment> Assignments { get; } = new List<ProjectAssignment>();

    public ICollection<PurchaseRequest> PurchaseRequests { get; } = new List<PurchaseRequest>();
}
