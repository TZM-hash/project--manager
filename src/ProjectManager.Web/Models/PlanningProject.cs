namespace ProjectManager.Web.Models;

/// <summary>
/// 规划中项目，用于跟踪尚未正式立项的项目意向和月度说明。
/// </summary>
public sealed class PlanningProject
{
    /// <summary>数据库主键。</summary>
    public int Id { get; set; }

    /// <summary>项目名。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>项目负责人用户 ID。</summary>
    public string? LeaderUserId { get; set; }

    /// <summary>项目负责人导航属性。</summary>
    public ApplicationUser? Leader { get; set; }

    /// <summary>厂商。</summary>
    public string? Vendor { get; set; }

    /// <summary>最新说明。</summary>
    public string? LatestDescription { get; set; }

    /// <summary>创建时间。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>最近更新时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>软删除标记。</summary>
    public bool IsDeleted { get; set; }

    /// <summary>月度结算历史记录。</summary>
    public ICollection<PlanningProjectHistoryRecord> HistoryRecords { get; } = new List<PlanningProjectHistoryRecord>();
}
