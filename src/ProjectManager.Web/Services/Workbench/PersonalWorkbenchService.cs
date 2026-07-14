using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services.Workbench;

public sealed class PersonalWorkbenchService(
    ApplicationDbContext db,
    TimeProvider timeProvider)
{
    public async Task<PersonalWorkbenchSnapshot> BuildAsync(
        string userId,
        bool canViewAll,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var staleBefore = timeProvider.GetUtcNow().AddDays(-30);
        var upcomingUntil = today.AddDays(14);

        var query = db.Projects
            .AsNoTracking()
            .AsSplitQuery()
            .Include(project => project.Status)
            .Include(project => project.Assignments)
            .Include(project => project.GanttPlan)
            .ThenInclude(plan => plan!.Tasks)
            .Where(project => !project.IsDeleted && project.Status != null && !project.Status.IsClosed);

        if (!canViewAll)
        {
            query = query.Where(project => project.Assignments.Any(assignment => assignment.UserId == userId));
        }

        var projects = await query.ToListAsync(cancellationToken);
        var overdue = projects
            .Where(project => project.GanttPlan?.FinishDate is { } finish &&
                              finish < today &&
                              project.ProgressPercent < 100)
            .OrderBy(project => project.GanttPlan!.FinishDate)
            .ThenBy(project => project.ProjectNumber)
            .ToArray();
        var pending = projects
            .Where(IsPending)
            .OrderByDescending(project => project.ProgressPercent - project.CollectionPercent)
            .ThenBy(project => project.ProjectNumber)
            .ToArray();
        var upcomingNodes = projects
            .SelectMany(project => (project.GanttPlan?.Tasks ?? [])
                .Where(task => task.ProgressPercent < 100 &&
                               task.PlannedFinishDate is { } finish &&
                               finish >= today && finish <= upcomingUntil)
                .Select(task => new WorkbenchNodeItem(
                    project.Id,
                    project.ProjectNumber,
                    project.Name,
                    task.Id,
                    task.Name,
                    task.PlannedFinishDate!.Value,
                    task.ProgressPercent)))
            .OrderBy(node => node.PlannedFinishDate)
            .ThenBy(node => node.ProjectNumber)
            .ToArray();
        var stale = projects
            .Where(project => project.UpdatedAt < staleBefore)
            .OrderBy(project => project.UpdatedAt)
            .ThenBy(project => project.ProjectNumber)
            .ToArray();

        var conclusion = BuildConclusion(overdue.Length, pending.Length, upcomingNodes.Length, stale.Length);
        return new PersonalWorkbenchSnapshot(
            conclusion.Title,
            conclusion.Description,
            conclusion.Tone,
            conclusion.ActionText,
            conclusion.ActionUrl,
            overdue.Length,
            pending.Length,
            upcomingNodes.Length,
            stale.Length,
            overdue.Take(5).Select(project => ToItem(project, "已逾期")).ToArray(),
            pending.Take(5).Select(project => ToItem(project, PendingReason(project))).ToArray(),
            upcomingNodes.Take(5).ToArray(),
            stale.Take(5).Select(project => ToItem(project, $"{(today.ToDateTime(TimeOnly.MinValue) - project.UpdatedAt.UtcDateTime).Days} 天未更新")).ToArray());
    }

    private static bool IsPending(Project project)
    {
        var code = project.Status?.Code ?? string.Empty;
        var name = project.Status?.Name ?? string.Empty;
        return code.Contains("wait", StringComparison.OrdinalIgnoreCase) ||
               code.Contains("block", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("等待", StringComparison.Ordinal) ||
               name.Contains("待處理", StringComparison.Ordinal) ||
               name.Contains("阻塞", StringComparison.Ordinal) ||
               project.CollectionPercent + 25 < project.ProgressPercent;
    }

    private static string PendingReason(Project project) =>
        project.CollectionPercent + 25 < project.ProgressPercent
            ? "收款進度落後"
            : project.Status?.Name ?? "待處理";

    private static WorkbenchProjectItem ToItem(Project project, string reason) => new(
        project.Id,
        project.ProjectNumber,
        project.Name,
        project.Status?.Name ?? "未設定",
        project.ProgressPercent,
        project.CollectionPercent,
        project.GanttPlan?.FinishDate,
        project.UpdatedAt,
        reason);

    private static WorkbenchConclusion BuildConclusion(
        int overdueCount,
        int pendingCount,
        int upcomingCount,
        int staleCount)
    {
        if (overdueCount > 0)
        {
            return new(
                $"有 {overdueCount} 個專案已逾期",
                "先處理已超過甘特計畫完成日、但尚未完成的專案。",
                "danger",
                "查看逾期專案",
                "/Workbench/Projects/Index?AnalysisType=overdue");
        }

        if (pendingCount > 0)
        {
            return new(
                $"有 {pendingCount} 個專案待處理",
                "等待、阻塞或收款進度落後的專案需要優先確認。",
                "warning",
                "查看待處理專案",
                "/Workbench/Projects/Index?AnalysisType=pending");
        }

        if (upcomingCount > 0)
        {
            return new(
                $"未來 14 天有 {upcomingCount} 個近期節點",
                "確認即將到期的甘特任務，避免進度滑落。",
                "info",
                "查看近期節點",
                "/Workbench/Projects/Index?AnalysisType=upcoming");
        }

        if (staleCount > 0)
        {
            return new(
                $"有 {staleCount} 個專案長期未更新",
                "超過 30 天未更新的專案需要補充目前狀態。",
                "warning",
                "查看長期未更新",
                "/Workbench/Projects/Index?AnalysisType=stale-update");
        }

        return new(
            "目前沒有高優先風險",
            "逾期、待處理、近期節點與長期未更新清單目前皆為空。",
            "success",
            "前往我的專案",
            "/Workbench/Projects/Index");
    }
}

public sealed record PersonalWorkbenchSnapshot(
    string HeroTitle,
    string HeroDescription,
    string HeroTone,
    string PrimaryActionText,
    string PrimaryActionUrl,
    int OverdueCount,
    int PendingCount,
    int UpcomingNodeCount,
    int StaleCount,
    IReadOnlyList<WorkbenchProjectItem> OverdueProjects,
    IReadOnlyList<WorkbenchProjectItem> PendingProjects,
    IReadOnlyList<WorkbenchNodeItem> UpcomingNodes,
    IReadOnlyList<WorkbenchProjectItem> StaleProjects);

public sealed record WorkbenchProjectItem(
    int ProjectId,
    string ProjectNumber,
    string Name,
    string StatusName,
    decimal ProgressPercent,
    decimal CollectionPercent,
    DateOnly? PlannedFinishDate,
    DateTimeOffset UpdatedAt,
    string Reason);

public sealed record WorkbenchNodeItem(
    int ProjectId,
    string ProjectNumber,
    string ProjectName,
    int TaskId,
    string TaskName,
    DateOnly PlannedFinishDate,
    decimal ProgressPercent);

internal sealed record WorkbenchConclusion(
    string Title,
    string Description,
    string Tone,
    string ActionText,
    string ActionUrl);
