namespace ProjectManager.Web.Models;

/// <summary>
/// 專案狀態字典，用于流程展示、结案判断和專案篩選。
/// </summary>
public sealed class ProjectStatus
{
    /// <summary>資料库主键。</summary>
    public int Id { get; set; }

    /// <summary>狀態编码，作为稳定业务标识。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>狀態名稱。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>流程時間线和下拉列表排序。</summary>
    public int SortOrder { get; set; }

    /// <summary>是否为结案狀態。</summary>
    public bool IsClosed { get; set; }

    /// <summary>是否啟用；停用狀態保留歷史專案顯示但不再允许新選擇。</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>狀態样式設定。</summary>
    public ProjectStatusStyle? Style { get; set; }
}
