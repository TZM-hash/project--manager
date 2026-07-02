using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Pages.Shared;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Workbench.Projects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader + "," + RoleNames.Viewer)]
public sealed class IndexModel(
    WorkbenchProjectService workbenchProjectService,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = PageSizeOptions.DefaultPageSize;

    public IReadOnlyList<Project> Projects { get; private set; } = [];

    public string CurrentUserId { get; private set; } = string.Empty;

    public bool CanEditAll { get; private set; }

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public IReadOnlyList<MetricInsight> Metrics { get; private set; } = [];

    public IReadOnlyList<ChartSlice> StatusSlices { get; private set; } = [];

    public IReadOnlyList<ChartSlice> ProgressSlices { get; private set; } = [];

    public PaginationViewModel Pagination => new(
        PageNumber,
        PageSize,
        TotalCount,
        TotalPages,
        "./Index",
        new Dictionary<string, string?>());

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        CurrentUserId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.IsInRole(RoleNames.Administrator) ||
                         User.IsInRole(RoleNames.Leader) ||
                         User.IsInRole(RoleNames.Viewer) ||
                         User.IsInRole(RoleNames.ProjectStaff);
        CanEditAll = User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.Leader);

        var page = await workbenchProjectService.GetProjectsForUserPageAsync(
            CurrentUserId,
            canViewAll,
            PageNumber,
            PageSize,
            cancellationToken);
        Projects = page.Items;
        TotalCount = page.TotalCount;
        PageNumber = page.PageNumber;
        PageSize = page.PageSize;
        TotalPages = page.TotalPages;
        await LoadInsightsAsync(CurrentUserId, canViewAll, cancellationToken);
    }

    private async Task LoadInsightsAsync(
        string currentUserId,
        bool canViewAll,
        CancellationToken cancellationToken)
    {
        var query = db.Projects
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!canViewAll)
        {
            query = query.Where(x => x.Assignments.Any(a => a.UserId == currentUserId));
        }

        var openCount = await query.CountAsync(x => x.Status != null && !x.Status.IsClosed, cancellationToken);
        var averageProgress = await query.AverageAsync(x => (decimal?)x.ProgressPercent, cancellationToken) ?? 0;
        var totalAmount = await query.SumAsync(x => (decimal?)x.ProjectAmount, cancellationToken) ?? 0;

        Metrics =
        [
            new MetricInsight("可见项目", TotalCount.ToString("N0"), "当前账号可查看"),
            new MetricInsight("未结案", openCount.ToString("N0"), "仍在推进"),
            new MetricInsight("项目金额", totalAmount.ToString("N2"), "可见范围汇总", "success"),
            new MetricInsight("平均进度", $"{averageProgress:0.#}%", "可见范围均值", "info")
        ];

        var statusRows = await query
            .GroupBy(x => x.Status == null ? "未设置" : x.Status.Name)
            .Select(x => new { Label = x.Key, Value = x.Count() })
            .OrderByDescending(x => x.Value)
            .ToListAsync(cancellationToken);
        StatusSlices = ChartPalette.BuildSlices(statusRows.Select(x => (x.Label, (decimal)x.Value)));

        var progressRows = new[]
        {
            ("0-29%", await query.CountAsync(x => x.ProgressPercent < 30, cancellationToken)),
            ("30-69%", await query.CountAsync(x => x.ProgressPercent >= 30 && x.ProgressPercent < 70, cancellationToken)),
            ("70-99%", await query.CountAsync(x => x.ProgressPercent >= 70 && x.ProgressPercent < 100, cancellationToken)),
            ("100%", await query.CountAsync(x => x.ProgressPercent >= 100, cancellationToken))
        };
        ProgressSlices = ChartPalette.BuildSlices(progressRows.Select(x => (x.Item1, (decimal)x.Item2)));
    }
}
