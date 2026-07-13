namespace ProjectManager.Web.Models;

/// <summary>
/// 月結明細快照。欄位儲存的是生成月結当时的專案、人员、请购彙總值。
/// </summary>
public sealed class MonthlySettlementItem
{
    /// <summary>資料库主键。</summary>
    public int Id { get; set; }

    /// <summary>所属月結批次 ID。</summary>
    public int BatchId { get; set; }

    /// <summary>所属月結批次導航屬性。</summary>
    public MonthlySettlementBatch? Batch { get; set; }

    /// <summary>来源專案 ID。</summary>
    public int ProjectId { get; set; }

    /// <summary>来源專案母案案號快照。</summary>
    public string? ParentCaseNumber { get; set; }

    /// <summary>来源專案工號快照。</summary>
    public string ProjectNumber { get; set; } = string.Empty;

    /// <summary>来源專案名稱快照。</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>来源專案人員名稱彙總快照。</summary>
    public string ProjectPersonnelText { get; set; } = string.Empty;

    /// <summary>来源專案進度快照。</summary>
    public decimal ProgressPercent { get; set; }

    /// <summary>来源專案金額快照。</summary>
    public decimal ProjectAmount { get; set; }

    /// <summary>来源專案收款比例快照。</summary>
    public decimal CollectionPercent { get; set; }

    /// <summary>来源專案狀態名稱快照。</summary>
    public string StatusName { get; set; } = string.Empty;

    /// <summary>来源專案狀態是否结案的快照。</summary>
    public bool IsClosed { get; set; }

    /// <summary>来源專案结案年月快照。</summary>
    public DateOnly? ClosedYearMonth { get; set; }

    /// <summary>来源專案請購號彙總快照。</summary>
    public string PurchaseRequestSummary { get; set; } = string.Empty;

    /// <summary>来源專案请购金額合計快照。</summary>
    public decimal PurchaseAmountTotal { get; set; }

    /// <summary>来源專案子案對接人員彙總快照。</summary>
    public string SubCaseContactSummary { get; set; } = string.Empty;

    /// <summary>来源專案付款比例彙總快照。</summary>
    public string PaymentPercentSummary { get; set; } = string.Empty;

    /// <summary>来源專案實際已付款合計快照。</summary>
    public decimal ActualPaidAmountTotal { get; set; }

    /// <summary>来源專案進度說明快照。</summary>
    public string? ProgressDescription { get; set; }

    /// <summary>来源專案最近更新人名稱快照。</summary>
    public string UpdatedByUserName { get; set; } = string.Empty;

    /// <summary>来源專案最近更新時間快照。</summary>
    public DateTimeOffset SourceUpdatedAt { get; set; }
}
