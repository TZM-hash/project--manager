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

namespace ProjectManager.Web.Pages.Reports.OpenProjects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.Leader + "," + RoleNames.Viewer + "," + RoleNames.ProjectStaff)]
public sealed class IndexModel(
    ProjectQueryService projectQueryService,
    ExcelReportService excelReportService,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : PageModel
{
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
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = PageSizeOptions.DefaultPageSize;

    public IReadOnlyList<Project> Projects { get; private set; } = [];

    public List<SelectListItem> StatusOptions { get; private set; } = [];

    public List<SelectListItem> UserOptions { get; private set; } = [];

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
        BuildRouteValues());

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadOptionsAsync(cancellationToken);
        var page = await projectQueryService.GetProjectsPageAsync(
            CreateFilter(),
            PageNumber,
            PageSize,
            cancellationToken);
        Projects = page.Items;
        TotalCount = page.TotalCount;
        PageNumber = page.PageNumber;
        PageSize = page.PageSize;
        TotalPages = page.TotalPages;
        await LoadInsightsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        var file = await excelReportService.ExportOpenProjectsAsync(CreateFilter(), cancellationToken);
        return File(file.Contents, file.ContentType, file.FileName);
    }

    private ProjectFilter CreateFilter()
    {
        var personnelUserId = User.IsInRole(RoleNames.ProjectStaff)
            ? userManager.GetUserId(User)
            : PersonnelUserId;

        return new ProjectFilter(
            Year,
            ParentCaseNumber,
            ProjectNumber,
            ProjectName,
            personnelUserId,
            StatusId,
            OpenOnly: true);
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        StatusOptions = await db.ProjectStatuses
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync(cancellationToken);

        var users = await userManager.Users
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        UserOptions = users
            .Select(x => new SelectListItem(
                string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? x.Id : x.DisplayName,
                x.Id))
            .ToList();
    }

    private async Task LoadInsightsAsync(CancellationToken cancellationToken)
    {
        var query = ApplyFilter(db.Projects.AsNoTracking().Where(x => !x.IsDeleted));
        var totalAmount = await query.SumAsync(x => (decimal?)x.ProjectAmount, cancellationToken) ?? 0;
        var purchaseAmount = await query
            .SelectMany(x => x.PurchaseRequests.Where(p => !p.IsDeleted))
            .SumAsync(x => (decimal?)x.PurchaseAmount, cancellationToken) ?? 0;
        var averageProgress = await query.AverageAsync(x => (decimal?)x.ProgressPercent, cancellationToken) ?? 0;

        Metrics =
        [
            new MetricInsight("未结案项目", TotalCount.ToString("N0"), "当前筛选结果"),
            new MetricInsight("项目金额", totalAmount.ToString("N2"), "未结案汇总", "success"),
            new MetricInsight("请购金额", purchaseAmount.ToString("N2"), "未结案请购汇总", "info"),
            new MetricInsight("平均进度", $"{averageProgress:0.#}%", "当前筛选均值")
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

        return query.Where(x => x.Status != null && !x.Status.IsClosed);
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
            [nameof(StatusId)] = StatusId?.ToString()
        };
    }
}
