using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ProjectManager.Web.Services;

public sealed class AuditLogService(ApplicationDbContext db)
{
    // 審計 JSON 需要保留中文，方便資料库排查和頁面直接展示。
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
        // 專案審計同时写入通用欄位和專案专用欄位，兼容旧日誌查詢与明細頁按專案查詢。
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
