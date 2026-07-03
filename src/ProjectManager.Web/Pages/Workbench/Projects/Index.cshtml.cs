using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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

    [BindProperty(SupportsGet = true)]
    public int? Year { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ParentCaseNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ProjectNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ProjectName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? PersonnelUserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? StatusId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool OpenOnly { get; set; }

    public IReadOnlyList<Project> Projects { get; private set; } = [];

    public string CurrentUserId { get; private set; } = string.Empty;

    public bool CanEditAll { get; private set; }

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public List<SelectListItem> StatusOptions { get; private set; } = [];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public IReadOnlyList<MetricInsight> Metrics { get; private set; } = [];

    public IReadOnlyList<ChartSlice> StatusSlices { get; private set; } = [];

    public IReadOnlyList<ChartSlice> ProgressSlices { get; private set; } = [];

    public PaginationViewModel Pagination => new(
        PageNumber,
        PageSize,
        TotalCount,
        TotalPages,
        "./Index",
        BuildRouteValues());

    public FilterSummaryViewModel FilterSummary => new(
        "./Index",
        BuildFilterSummaryItems(),
        new Dictionary<string, string?> { [nameof(PageSize)] = PageSize.ToString() });

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadOptionsAsync(cancellationToken);
        CurrentUserId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.IsInRole(RoleNames.Administrator) ||
                         User.IsInRole(RoleNames.Leader) ||
                         User.IsInRole(RoleNames.Viewer) ||
                         User.IsInRole(RoleNames.ProjectStaff);
        CanEditAll = User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.Leader);

        var page = await workbenchProjectService.GetProjectsForUserPageAsync(
            CurrentUserId,
            canViewAll,
            CreateFilter(),
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

    private ProjectFilter CreateFilter()
    {
        return new ProjectFilter(
            Year,
            ParentCaseNumber,
            ProjectNumber,
            ProjectName,
            PersonnelUserId,
            StatusId,
            OpenOnly);
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        StatusOptions = await db.ProjectStatuses
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync(cancellationToken);

        UserOptions = await userManager.Users
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserName)
            .Select(x => new SelectListItem(
                string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? x.Id : x.DisplayName,
                x.Id))
            .ToListAsync(cancellationToken);
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

        query = ApplyFilter(query);
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

    private IQueryable<Project> ApplyFilter(IQueryable<Project> query)
    {
        var filter = CreateFilter();
        if (filter.Year is not null)
        {
            query = query.Where(x => x.Year == filter.Year);
        }

        if (!string.IsNullOrWhiteSpace(filter.ParentCaseNumber))
        {
            query = query.Where(x => x.ParentCaseNumber != null &&
                                     x.ParentCaseNumber.Contains(filter.ParentCaseNumber));
        }

        if (!string.IsNullOrWhiteSpace(filter.ProjectNumber))
        {
            query = query.Where(x => x.ProjectNumber.Contains(filter.ProjectNumber));
        }

        if (!string.IsNullOrWhiteSpace(filter.ProjectName))
        {
            query = query.Where(x => x.Name.Contains(filter.ProjectName));
        }

        if (!string.IsNullOrWhiteSpace(filter.PersonnelUserId))
        {
            query = query.Where(x => x.Assignments.Any(a => a.UserId == filter.PersonnelUserId));
        }

        if (filter.StatusId is not null)
        {
            query = query.Where(x => x.StatusId == filter.StatusId);
        }

        if (filter.OpenOnly)
        {
            query = query.Where(x => x.Status != null && !x.Status.IsClosed);
        }

        return query;
    }

    private Dictionary<string, string?> BuildRouteValues()
    {
        return new Dictionary<string, string?>
        {
            [nameof(Year)] = Year?.ToString(),
            [nameof(ParentCaseNumber)] = ParentCaseNumber,
            [nameof(ProjectNumber)] = ProjectNumber,
            [nameof(ProjectName)] = ProjectName,
            [nameof(PersonnelUserId)] = PersonnelUserId,
            [nameof(StatusId)] = StatusId?.ToString(),
            [nameof(OpenOnly)] = OpenOnly.ToString()
        };
    }

    private IReadOnlyList<FilterSummaryItem> BuildFilterSummaryItems()
    {
        var items = new List<FilterSummaryItem>();
        if (Year is not null)
        {
            items.Add(new FilterSummaryItem("年", Year.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(ParentCaseNumber))
        {
            items.Add(new FilterSummaryItem("母档案号", ParentCaseNumber));
        }

        if (!string.IsNullOrWhiteSpace(ProjectNumber))
        {
            items.Add(new FilterSummaryItem("项目工号", ProjectNumber));
        }

        if (!string.IsNullOrWhiteSpace(ProjectName))
        {
            items.Add(new FilterSummaryItem("项目名称", ProjectName));
        }

        if (!string.IsNullOrWhiteSpace(PersonnelUserId))
        {
            var userText = UserOptions.FirstOrDefault(x => x.Value == PersonnelUserId)?.Text ?? PersonnelUserId;
            items.Add(new FilterSummaryItem("专案人员", userText));
        }

        if (StatusId is not null)
        {
            var statusText = StatusOptions.FirstOrDefault(x => x.Value == StatusId.Value.ToString())?.Text ?? StatusId.Value.ToString();
            items.Add(new FilterSummaryItem("状态", statusText));
        }

        if (OpenOnly)
        {
            items.Add(new FilterSummaryItem("范围", "未结案"));
        }

        return items;
    }
}
