namespace ProjectManager.Web.Models;

/// <summary>
/// 请购类型。数据库中以整数保存，页面上显示为内购或外购。
/// </summary>
public enum PurchaseType
{
    /// <summary>内购。</summary>
    InternalPurchase = 1,

    /// <summary>外购。</summary>
    ExternalPurchase = 2
}

/// <summary>
/// 项目下的请购记录，记录请购号、金额、付款比例和实际已付款等信息。
/// </summary>
public sealed class PurchaseRequest
{
    /// <summary>数据库主键。</summary>
    public int Id { get; set; }

    /// <summary>所属项目 ID。</summary>
    public int ProjectId { get; set; }

    /// <summary>所属项目导航属性。</summary>
    public Project? Project { get; set; }

    /// <summary>请购号。</summary>
    public string RequestNumber { get; set; } = string.Empty;

    /// <summary>请购类型。</summary>
    public PurchaseType PurchaseType { get; set; }

    /// <summary>请购人员用户 ID。</summary>
    public string? PurchaseStaffUserId { get; set; }

    /// <summary>请购人员导航属性。</summary>
    public ApplicationUser? PurchaseStaff { get; set; }

    /// <summary>请购金额。</summary>
    public decimal PurchaseAmount { get; set; }

    /// <summary>子案对接人员用户 ID。</summary>
    public string? SubCaseContactUserId { get; set; }

    /// <summary>子案对接人员导航属性。</summary>
    public ApplicationUser? SubCaseContact { get; set; }

    /// <summary>付款比例百分比。</summary>
    public decimal PaymentPercent { get; set; }

    /// <summary>实际已付款金额。</summary>
    public decimal ActualPaidAmount { get; set; }

    /// <summary>请购备注。</summary>
    public string? Notes { get; set; }

    /// <summary>创建时间。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>最近更新时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>软删除标记。</summary>
    public bool IsDeleted { get; set; }
}
