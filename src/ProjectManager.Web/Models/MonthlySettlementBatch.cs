namespace ProjectManager.Web.Models;

/// <summary>
/// 月結批次头。每次生成月結都会建立一个批次，批次号在同年同月内递增。
/// </summary>
public sealed class MonthlySettlementBatch
{
    /// <summary>資料库主键。</summary>
    public int Id { get; set; }

    /// <summary>月結年度。</summary>
    public int Year { get; set; }

    /// <summary>月結月份。</summary>
    public int Month { get; set; }

    /// <summary>同年同月内的批次号。</summary>
    public int BatchNumber { get; set; }

    /// <summary>建立人使用者 ID。</summary>
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>建立人導航屬性。</summary>
    public ApplicationUser? CreatedByUser { get; set; }

    /// <summary>建立時間。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>批次備註。</summary>
    public string? Notes { get; set; }

    /// <summary>该批次下的月結快照明細。</summary>
    public ICollection<MonthlySettlementItem> Items { get; } = new List<MonthlySettlementItem>();
}
