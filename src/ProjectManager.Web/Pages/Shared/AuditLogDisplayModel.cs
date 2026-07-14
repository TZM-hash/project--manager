using System.Text.Json;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Shared;

public sealed record AuditLogDisplayModel(
    DateTimeOffset CreatedAt,
    string Actor,
    string Action,
    string Summary,
    IReadOnlyList<AuditChangeDetail> Details,
    string ActionValue = "")
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<AuditLogDisplayModel> FromLogs(IEnumerable<AuditLog> logs)
    {
        return logs.Select(log => new AuditLogDisplayModel(
                log.CreatedAt,
                DisplayActor(log),
                GetActionLabel(log.Action),
                string.IsNullOrWhiteSpace(log.ChangeSummary) ? log.Description : log.ChangeSummary,
                DeserializeDetails(log.ChangeDetailsJson),
                log.Action))
            .ToList();
    }

    public static string FormatDetail(AuditChangeDetail detail)
    {
        return detail.Category switch
        {
            "PurchaseAdded" => $"{detail.Scope}：{detail.After}",
            "PurchaseDeleted" => $"{detail.Scope}：{detail.Before}",
            "ProjectDeleted" => detail.Before ?? "專案已刪除",
            _ => $"{detail.Scope} / {detail.Label}：{DisplayValue(detail.Before)} -> {DisplayValue(detail.After)}"
        };
    }

    public static string GetActionLabel(string action)
    {
        return action switch
        {
            "Create" => "新增",
            "Update" => "修改",
            "Delete" => "刪除",
            "ProgressUpdate" => "更新進度",
            _ => action
        };
    }

    private static IReadOnlyList<AuditChangeDetail> DeserializeDetails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<AuditChangeDetail>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string DisplayActor(AuditLog log)
    {
        return log.User?.DisplayName
            ?? log.User?.UserName
            ?? log.User?.Email
            ?? log.UserId
            ?? "系统";
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "空" : value;
    }
}
