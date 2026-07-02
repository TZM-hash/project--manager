namespace ProjectManager.Web.Models;

/// <summary>
/// 月结明细快照。字段保存的是生成月结当时的项目、人员、请购汇总值。
/// </summary>
public sealed class MonthlySettlementItem
{
    /// <summary>数据库主键。</summary>
    public int Id { get; set; }

    /// <summary>所属月结批次 ID。</summary>
    public int BatchId { get; set; }

    /// <summary>所属月结批次导航属性。</summary>
    public MonthlySettlementBatch? Batch { get; set; }

    /// <summary>来源项目 ID。</summary>
    public int ProjectId { get; set; }

    /// <summary>来源项目母案案号快照。</summary>
    public string? ParentCaseNumber { get; set; }

    /// <summary>来源项目工号快照。</summary>
    public string ProjectNumber { get; set; } = string.Empty;

    /// <summary>来源项目名称快照。</summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>来源项目人员名称汇总快照。</summary>
    public string ProjectPersonnelText { get; set; } = string.Empty;

    /// <summary>来源项目进度快照。</summary>
    public decimal ProgressPercent { get; set; }

    /// <summary>来源项目金额快照。</summary>
    public decimal ProjectAmount { get; set; }

    /// <summary>来源项目收款比例快照。</summary>
    public decimal CollectionPercent { get; set; }

    /// <summary>来源项目状态名称快照。</summary>
    public string StatusName { get; set; } = string.Empty;

    /// <summary>来源项目状态是否结案的快照。</summary>
    public bool IsClosed { get; set; }

    /// <summary>来源项目结案年月快照。</summary>
    public DateOnly? ClosedYearMonth { get; set; }

    /// <summary>来源项目请购号汇总快照。</summary>
    public string PurchaseRequestSummary { get; set; } = string.Empty;

    /// <summary>来源项目请购金额合计快照。</summary>
    public decimal PurchaseAmountTotal { get; set; }

    /// <summary>来源项目子案对接人员汇总快照。</summary>
    public string SubCaseContactSummary { get; set; } = string.Empty;

    /// <summary>来源项目付款比例汇总快照。</summary>
    public string PaymentPercentSummary { get; set; } = string.Empty;

    /// <summary>来源项目实际已付款合计快照。</summary>
    public decimal ActualPaidAmountTotal { get; set; }

    /// <summary>来源项目进度说明快照。</summary>
    public string? ProgressDescription { get; set; }

    /// <summary>来源项目最近更新人名称快照。</summary>
    public string UpdatedByUserName { get; set; } = string.Empty;

    /// <summary>来源项目最近更新时间快照。</summary>
    public DateTimeOffset SourceUpdatedAt { get; set; }
}
