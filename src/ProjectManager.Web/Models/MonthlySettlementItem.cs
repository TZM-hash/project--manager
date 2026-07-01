namespace ProjectManager.Web.Models;

public sealed class MonthlySettlementItem
{
    public int Id { get; set; }

    public int BatchId { get; set; }

    public MonthlySettlementBatch? Batch { get; set; }

    public int ProjectId { get; set; }

    public string? ParentCaseNumber { get; set; }

    public string ProjectNumber { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string ProjectPersonnelText { get; set; } = string.Empty;

    public decimal ProgressPercent { get; set; }

    public decimal ProjectAmount { get; set; }

    public decimal CollectionPercent { get; set; }

    public string StatusName { get; set; } = string.Empty;

    public bool IsClosed { get; set; }

    public DateOnly? ClosedYearMonth { get; set; }

    public string PurchaseRequestSummary { get; set; } = string.Empty;

    public decimal PurchaseAmountTotal { get; set; }

    public string SubCaseContactSummary { get; set; } = string.Empty;

    public string PaymentPercentSummary { get; set; } = string.Empty;

    public decimal ActualPaidAmountTotal { get; set; }

    public string? ProgressDescription { get; set; }

    public string UpdatedByUserName { get; set; } = string.Empty;

    public DateTimeOffset SourceUpdatedAt { get; set; }
}
