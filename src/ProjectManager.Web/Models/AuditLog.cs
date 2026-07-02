namespace ProjectManager.Web.Models;

/// <summary>
/// 系统操作日志。项目相关日志会额外保存项目 ID、项目工号和结构化变更明细。
/// </summary>
public sealed class AuditLog
{
    /// <summary>数据库主键。</summary>
    public int Id { get; set; }

    /// <summary>操作人用户 ID；系统自动操作时可为空。</summary>
    public string? UserId { get; set; }

    /// <summary>操作人导航属性。</summary>
    public ApplicationUser? User { get; set; }

    /// <summary>操作类型，例如 Create、Update、Delete、ProgressUpdate。</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>被操作实体名称；项目留痕固定为 Project。</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>被操作实体 ID，保留字符串格式兼容通用审计。</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>通用文字描述，兼容早期审计日志。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>项目 ID，便于详情页直接查询该项目的操作记录。</summary>
    public int? ProjectId { get; set; }

    /// <summary>项目工号快照，避免项目工号变化后历史记录丢失上下文。</summary>
    public string? ProjectNumber { get; set; }

    /// <summary>人可读的变更摘要。</summary>
    public string? ChangeSummary { get; set; }

    /// <summary>字段级和请购明细级变更 JSON。</summary>
    public string? ChangeDetailsJson { get; set; }

    /// <summary>操作发生时间。</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
