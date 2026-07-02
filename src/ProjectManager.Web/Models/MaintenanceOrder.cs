namespace ProjectManager.Web.Models;

public enum MaintenanceMethod
{
    OnSite = 1,
    Remote = 2,
    Both = 3
}

public sealed class MaintenanceOrder
{
    public int Id { get; set; }

    public int Year { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public DateOnly MaintenanceStartDate { get; set; }

    public DateOnly MaintenanceEndDate { get; set; }

    public MaintenanceMethod MaintenanceMethod { get; set; }

    public int OnSiteAnnualCount { get; set; }

    public int RemoteAnnualCount { get; set; }

    public string? ExecutorUserId { get; set; }

    public ApplicationUser? Executor { get; set; }

    public decimal HandoverPercent { get; set; }

    public string? UpdatedByUserId { get; set; }

    public ApplicationUser? UpdatedByUser { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsDeleted { get; set; }
}
