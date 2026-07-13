namespace ProjectManager.Web.Models;

/// <summary>
/// 规划中專案的月度結算歷史記錄。
/// </summary>
public sealed class PlanningProjectHistoryRecord
{
    /// <summary>資料库主键。</summary>
    public int Id { get; set; }

    /// <summary>所属规划中專案 ID。</summary>
    public int PlanningProjectId { get; set; }

    /// <summary>所属规划中專案導航屬性。</summary>
    public PlanningProject? PlanningProject { get; set; }

    /// <summary>記錄年份。</summary>
    public int Year { get; set; }

    /// <summary>記錄月份。</summary>
    public int Month { get; set; }

    /// <summary>上期說明。</summary>
    public string? PreviousDescription { get; set; }

    /// <summary>本期記錄。</summary>
    public string? CurrentRecord { get; set; }

    /// <summary>建立人员使用者 ID。</summary>
    public string? CreatedByUserId { get; set; }

    /// <summary>建立人员導航屬性。</summary>
    public ApplicationUser? CreatedByUser { get; set; }

    /// <summary>建立時間。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
