namespace ProjectManager.Web.Models;

public enum PurchaseType
{
    InternalPurchase = 1,
    ExternalPurchase = 2
}

public sealed class PurchaseRequest
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    public string RequestNumber { get; set; } = string.Empty;

    public PurchaseType PurchaseType { get; set; }

    public string? PurchaseStaffUserId { get; set; }

    public ApplicationUser? PurchaseStaff { get; set; }

    public decimal PurchaseAmount { get; set; }

    public string? SubCaseContactUserId { get; set; }

    public ApplicationUser? SubCaseContact { get; set; }

    public decimal PaymentPercent { get; set; }

    public decimal ActualPaidAmount { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsDeleted { get; set; }
}
