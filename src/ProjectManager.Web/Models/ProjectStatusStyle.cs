namespace ProjectManager.Web.Models;

/// <summary>
/// 專案狀態的頁面顯示样式。
/// </summary>
public sealed class ProjectStatusStyle
{
    /// <summary>資料库主键。</summary>
    public int Id { get; set; }

    /// <summary>狀態 ID，一对一关联 ProjectStatus。</summary>
    public int StatusId { get; set; }

    /// <summary>狀態導航屬性。</summary>
    public ProjectStatus? Status { get; set; }

    /// <summary>狀態徽标文字颜色。</summary>
    public string TextColor { get; set; } = "#1f2937";

    /// <summary>狀態徽标背景颜色。</summary>
    public string BackgroundColor { get; set; } = "#e5e7eb";

    /// <summary>是否加粗顯示。</summary>
    public bool IsBold { get; set; }
}
