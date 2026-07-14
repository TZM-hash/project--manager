using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public sealed record ProjectVisualState(string CssKey, string Label, string Description);

public static class ProjectVisualStateResolver
{
    private static readonly HashSet<string> ActiveCodes =
    [
        "purchaserequested",
        "coding",
        "trialrun"
    ];

    private static readonly HashSet<string> WaitingCodes =
    [
        "waitingcollection",
        "pendingclosure"
    ];

    public static ProjectVisualState ResolveStatus(string? code, bool isClosed)
    {
        var normalizedCode = Normalize(code);
        if (isClosed || normalizedCode == "closed")
        {
            return new("complete", "已完成", "專案已結案");
        }

        if (normalizedCode.Contains("blocked", StringComparison.Ordinal)
            || normalizedCode.Contains("stopped", StringComparison.Ordinal))
        {
            return new("blocked", "受阻", "專案目前受阻");
        }

        if (WaitingCodes.Contains(normalizedCode))
        {
            return new("waiting", "等待中", "專案正在等待後續處理");
        }

        if (ActiveCodes.Contains(normalizedCode))
        {
            return new("active", "推進中", "專案正在執行");
        }

        return new("neutral", "一般", "一般專案狀態");
    }

    public static ProjectVisualState ResolveRisk(
        Project project,
        DateTimeOffset now,
        TimeSpan? staleAfter = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        var progress = Math.Clamp(project.ProgressPercent, 0, 100);
        var collection = Math.Clamp(project.CollectionPercent, 0, 100);
        var status = ResolveStatus(project.Status?.Code, project.Status?.IsClosed == true);

        if (status.CssKey == "complete" || progress >= 100)
        {
            return new("complete", "已完成", project.Status?.IsClosed == true ? "狀態已結案" : "進度已達 100%");
        }

        if (status.CssKey == "blocked")
        {
            return status;
        }

        if (collection + 25 < progress)
        {
            return new("warning", "收款落後", $"進度 {progress:0.##}%／收款 {collection:0.##}%");
        }

        var staleLimit = staleAfter ?? TimeSpan.FromDays(30);
        if (now - project.UpdatedAt > staleLimit)
        {
            return new("attention", "久未更新", $"已超過 {staleLimit.Days} 天未更新");
        }

        return new("normal", "推進中", $"{project.Status?.Name ?? "未設定狀態"}／進度 {progress:0.##}%");
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty)
            .Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
    }
}
