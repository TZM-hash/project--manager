namespace ProjectManager.Web.Models;

/// <summary>
/// 系统操作日誌。專案相关日誌会额外儲存專案 ID、專案工號和结构化变更明細。
/// </summary>
public sealed class AuditLog
{
    /// <summary>資料库主键。</summary>
    public int Id { get; set; }

    /// <summary>操作人使用者 ID；系统自动操作时可为空。</summary>
    public string? UserId { get; set; }

    /// <summary>操作人導航屬性。</summary>
    public ApplicationUser? User { get; set; }

    /// <summary>操作類型，例如 Create、Update、Delete、ProgressUpdate。</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>被操作实体名稱；專案留痕固定为 Project。</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>被操作实体 ID，保留字符串格式兼容通用審計。</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>通用文字描述，兼容早期審計日誌。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>專案 ID，便于明細頁直接查詢该專案的操作記錄。</summary>
    public int? ProjectId { get; set; }

    /// <summary>專案工號快照，避免專案工號变化后歷史記錄丢失上下文。</summary>
    public string? ProjectNumber { get; set; }

    /// <summary>人可读的变更摘要。</summary>
    public string? ChangeSummary { get; set; }

    /// <summary>欄位级和请购明細级变更 JSON。</summary>
    public string? ChangeDetailsJson { get; set; }

    /// <summary>操作发生時間。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
