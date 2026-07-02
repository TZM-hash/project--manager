namespace ProjectManager.Web.Models;

/// <summary>
/// 项目状态字典，用于流程展示、结案判断和项目筛选。
/// </summary>
public sealed class ProjectStatus
{
    /// <summary>数据库主键。</summary>
    public int Id { get; set; }

    /// <summary>状态编码，作为稳定业务标识。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>状态名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>流程时间线和下拉列表排序。</summary>
    public int SortOrder { get; set; }

    /// <summary>是否为结案状态。</summary>
    public bool IsClosed { get; set; }

    /// <summary>是否启用；停用状态保留历史项目显示但不再允许新选择。</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>状态样式配置。</summary>
    public ProjectStatusStyle? Style { get; set; }
}
