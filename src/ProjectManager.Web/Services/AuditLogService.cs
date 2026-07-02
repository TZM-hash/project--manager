using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ProjectManager.Web.Services;

public sealed class AuditLogService(ApplicationDbContext db)
{
    // 审计 JSON 需要保留中文，方便数据库排查和页面直接展示。
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async Task LogAsync(
        string? userId,
        string action,
        string entityName,
        string entityId,
        string description,
        CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LogProjectChangeAsync(
        string? userId,
        string action,
        int projectId,
        string projectNumber,
        string changeSummary,
        IReadOnlyCollection<AuditChangeDetail> details,
        CancellationToken cancellationToken)
    {
        // 项目审计同时写入通用字段和项目专用字段，兼容旧日志查询与详情页按项目查询。
        db.AuditLogs.Add(new AuditLog
        {
            UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
            Action = action,
            EntityName = "Project",
            EntityId = projectId.ToString(),
            Description = changeSummary,
            ProjectId = projectId,
            ProjectNumber = projectNumber,
            ChangeSummary = changeSummary,
            ChangeDetailsJson = JsonSerializer.Serialize(details, JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record AuditChangeDetail(
    string Category,
    string Label,
    string? Before,
    string? After,
    string Scope);
