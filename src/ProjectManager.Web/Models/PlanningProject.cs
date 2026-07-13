namespace ProjectManager.Web.Models;

/// <summary>
/// 规划中專案，用于跟踪尚未正式立项的專案意向和月度說明。
/// </summary>
public sealed class PlanningProject
{
    /// <summary>資料库主键。</summary>
    public int Id { get; set; }

    /// <summary>專案名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>專案負責人使用者 ID。</summary>
    public string? LeaderUserId { get; set; }

    /// <summary>專案負責人導航屬性。</summary>
    public ApplicationUser? Leader { get; set; }

    /// <summary>廠商。</summary>
    public string? Vendor { get; set; }

    /// <summary>最新說明。</summary>
    public string? LatestDescription { get; set; }

    /// <summary>建立時間。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>最近更新時間。</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>软刪除标记。</summary>
    public bool IsDeleted { get; set; }

    /// <summary>月度結算歷史記錄。</summary>
    public ICollection<PlanningProjectHistoryRecord> HistoryRecords { get; } = new List<PlanningProjectHistoryRecord>();
}
