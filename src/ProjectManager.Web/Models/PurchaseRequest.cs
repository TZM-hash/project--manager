namespace ProjectManager.Web.Models;

/// <summary>
/// 请购類型。資料库中以整数儲存，頁面上顯示为內購或外購。
/// </summary>
public enum PurchaseType
{
    /// <summary>內購。</summary>
    InternalPurchase = 1,

    /// <summary>外購。</summary>
    ExternalPurchase = 2
}

/// <summary>
/// 專案下的請購記錄，記錄請購號、金額、付款比例和實際已付款等資訊。
/// </summary>
public sealed class PurchaseRequest
{
    /// <summary>資料库主键。</summary>
    public int Id { get; set; }

    /// <summary>所属專案 ID。</summary>
    public int ProjectId { get; set; }

    /// <summary>所属專案導航屬性。</summary>
    public Project? Project { get; set; }

    /// <summary>請購號。</summary>
    public string RequestNumber { get; set; } = string.Empty;

    /// <summary>请购類型。</summary>
    public PurchaseType PurchaseType { get; set; }

    /// <summary>請購人員使用者 ID。</summary>
    public string? PurchaseStaffUserId { get; set; }

    /// <summary>請購人員導航屬性。</summary>
    public ApplicationUser? PurchaseStaff { get; set; }

    /// <summary>请购金額。</summary>
    public decimal PurchaseAmount { get; set; }

    /// <summary>子案對接人員使用者 ID。</summary>
    public string? SubCaseContactUserId { get; set; }

    /// <summary>子案對接人員導航屬性。</summary>
    public ApplicationUser? SubCaseContact { get; set; }

    /// <summary>廠商聯絡人 ID。</summary>
    public int? VendorContactId { get; set; }

    /// <summary>廠商聯絡人導航屬性。</summary>
    public VendorContact? VendorContact { get; set; }

    /// <summary>付款比例百分比。</summary>
    public decimal PaymentPercent { get; set; }

    /// <summary>實際已付款金額。</summary>
    public decimal ActualPaidAmount { get; set; }

    /// <summary>请购備註。</summary>
    public string? Notes { get; set; }

    /// <summary>建立時間。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>最近更新時間。</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>软刪除标记。</summary>
    public bool IsDeleted { get; set; }
}
