namespace ProjectManager.Web.Models;

/// <summary>
/// 项目主数据。所有项目列表、进度、请购、月结和审计记录都围绕该实体展开。
/// </summary>
public sealed class Project
{
    /// <summary>数据库主键。</summary>
    public int Id { get; set; }

    /// <summary>项目年度；与项目工号组成未删除项目的唯一业务键。</summary>
    public int Year { get; set; }

    /// <summary>母案案号，可为空。</summary>
    public string? ParentCaseNumber { get; set; }

    /// <summary>项目工号，同一年度内未删除项目不可重复。</summary>
    public string ProjectNumber { get; set; } = string.Empty;

    /// <summary>项目名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>项目进度百分比，业务规则限制在 0 到 100。</summary>
    public decimal ProgressPercent { get; set; }

    /// <summary>项目金额。</summary>
    public decimal ProjectAmount { get; set; }

    /// <summary>收款比例百分比。</summary>
    public decimal CollectionPercent { get; set; }

    /// <summary>进度补充说明。</summary>
    public string? ProgressDescription { get; set; }

    /// <summary>当前项目状态外键。</summary>
    public int StatusId { get; set; }

    /// <summary>当前项目状态导航属性。</summary>
    public ProjectStatus? Status { get; set; }

    /// <summary>最近更新人的用户 ID。</summary>
    public string? UpdatedByUserId { get; set; }

    /// <summary>最近更新人导航属性。</summary>
    public ApplicationUser? UpdatedByUser { get; set; }

    /// <summary>结案年月；保存时统一归一到该月第一天。</summary>
    public DateOnly? ClosedYearMonth { get; set; }

    /// <summary>最近更新时间。</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>创建时间。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>软删除标记；列表和详情查询默认过滤已删除项目。</summary>
    public bool IsDeleted { get; set; }

    /// <summary>分配到该项目的专案人员。</summary>
    public ICollection<ProjectAssignment> Assignments { get; } = new List<ProjectAssignment>();

    /// <summary>该项目下的请购记录。</summary>
    public ICollection<PurchaseRequest> PurchaseRequests { get; } = new List<PurchaseRequest>();

    public ProjectGanttPlan? GanttPlan { get; set; }
}
