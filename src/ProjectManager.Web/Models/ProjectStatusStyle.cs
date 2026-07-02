namespace ProjectManager.Web.Models;

/// <summary>
/// 项目状态的页面显示样式。
/// </summary>
public sealed class ProjectStatusStyle
{
    /// <summary>数据库主键。</summary>
    public int Id { get; set; }

    /// <summary>状态 ID，一对一关联 ProjectStatus。</summary>
    public int StatusId { get; set; }

    /// <summary>状态导航属性。</summary>
    public ProjectStatus? Status { get; set; }

    /// <summary>状态徽标文字颜色。</summary>
    public string TextColor { get; set; } = "#1f2937";

    /// <summary>状态徽标背景颜色。</summary>
    public string BackgroundColor { get; set; } = "#e5e7eb";

    /// <summary>是否加粗显示。</summary>
    public bool IsBold { get; set; }
}
