namespace ProjectManager.Web.Models;

/// <summary>
/// 规划中项目的月度结算历史记录。
/// </summary>
public sealed class PlanningProjectHistoryRecord
{
    /// <summary>数据库主键。</summary>
    public int Id { get; set; }

    /// <summary>所属规划中项目 ID。</summary>
    public int PlanningProjectId { get; set; }

    /// <summary>所属规划中项目导航属性。</summary>
    public PlanningProject? PlanningProject { get; set; }

    /// <summary>记录年份。</summary>
    public int Year { get; set; }

    /// <summary>记录月份。</summary>
    public int Month { get; set; }

    /// <summary>上期说明。</summary>
    public string? PreviousDescription { get; set; }

    /// <summary>本期记录。</summary>
    public string? CurrentRecord { get; set; }

    /// <summary>创建人员用户 ID。</summary>
    public string? CreatedByUserId { get; set; }

    /// <summary>创建人员导航属性。</summary>
    public ApplicationUser? CreatedByUser { get; set; }

    /// <summary>创建时间。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
