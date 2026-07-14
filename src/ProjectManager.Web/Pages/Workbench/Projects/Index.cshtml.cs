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
using ProjectType = ProjectManager.Web.Models.ProjectType;
using ProjectManager.Web.Services.DataViews;

namespace ProjectManager.Web.Pages.Workbench.Projects;

[Authorize]
public sealed class IndexModel(
    WorkbenchProjectService workbenchProjectService,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    SavedDataViewPageSupport savedDataViews) : PageModel
{
    private const string DataViewPageKey = "workbench-projects";

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
    public ProjectType? ProjectType { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool OpenOnly { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AnalysisType { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ViewPreset { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? SavedViewId { get; set; }

    [BindProperty]
    public SaveDataViewInput Input { get; set; } = new();

    public SavedDataViewBarViewModel SavedViewBar { get; private set; } = null!;

    public IReadOnlyList<Project> Projects { get; private set; } = [];

    public string CurrentUserId { get; private set; } = string.Empty;

    public bool CanEditAll { get; private set; }

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public decimal TotalProjectAmount { get; private set; }

    public List<SelectListItem> StatusOptions { get; private set; } = [];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public List<SelectListItem> ProjectTypeOptions { get; } =
    [
        new("保養", ((int)Models.ProjectType.Maintenance).ToString()),
        new("工程", ((int)Models.ProjectType.Engineering).ToString())
    ];

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
        CurrentUserId = userManager.GetUserId(User) ?? string.Empty;
        await ResolveSavedViewAsync(cancellationToken);
        await LoadOptionsAsync(cancellationToken);
        CanEditAll = User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.Leader);
        var canViewAll = CanEditAll || User.IsInRole(RoleNames.Viewer);

        if (!canViewAll && PersonnelUserId == null)
        {
            PersonnelUserId = CurrentUserId;
        }

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
        TotalProjectAmount = Projects.Sum(x => x.ProjectAmount);
        await LoadInsightsAsync(CurrentUserId, canViewAll, cancellationToken);
    }

    public async Task<IActionResult> OnPostSaveViewAsync(CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? throw new InvalidOperationException("找不到目前使用者。");
        try
        {
            var saved = await savedDataViews.SaveAsync(userId, DataViewPageKey, Input, cancellationToken);
            TempData["SuccessMessage"] = $"已儲存個人檢視「{saved.Name}」。";
        }
        catch (ArgumentException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        return RedirectToLocal(Input.ReturnUrl);
    }

    public async Task<IActionResult> OnPostDeleteViewAsync(int id, string returnUrl, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? throw new InvalidOperationException("找不到目前使用者。");
        var deleted = await savedDataViews.DeleteAsync(userId, id, cancellationToken);
        TempData[deleted ? "SuccessMessage" : "ErrorMessage"] = deleted ? "個人檢視已刪除。" : "找不到可刪除的個人檢視。";
        return RedirectToLocal(returnUrl);
    }

    public async Task<IActionResult> OnPostSetDefaultViewAsync(int id, string returnUrl, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? throw new InvalidOperationException("找不到目前使用者。");
        var updated = await savedDataViews.SetDefaultAsync(userId, id, cancellationToken);
        TempData[updated ? "SuccessMessage" : "ErrorMessage"] = updated ? "預設檢視已更新。" : "找不到可設定的個人檢視。";
        return RedirectToLocal(returnUrl);
    }

    private async Task ResolveSavedViewAsync(CancellationToken cancellationToken)
    {
        var explicitFilters = new Dictionary<string, string?>
        {
            ["Year"] = Year?.ToString(),
            ["ParentCaseNumber"] = ParentCaseNumber,
            ["ProjectNumber"] = ProjectNumber,
            ["Name"] = ProjectName,
            ["PersonnelUserId"] = PersonnelUserId,
            ["StatusId"] = StatusId?.ToString(),
            ["ProjectType"] = ProjectType.HasValue ? ((int)ProjectType.Value).ToString() : null,
            ["OpenOnly"] = OpenOnly.ToString(),
            ["AnalysisType"] = AnalysisType
        };
        var filterKeys = new[] { "Year", "ParentCaseNumber", "ProjectNumber", "ProjectName", "Name", "PersonnelUserId", "StatusId", "ProjectType", "OpenOnly", "AnalysisType" };
        var hasExplicitFilters = filterKeys.Any(key => Request.Query.ContainsKey(key));
        var resolved = await savedDataViews.ResolveAsync(
            CurrentUserId,
            DataViewPageKey,
            ViewPreset,
            SavedViewId,
            explicitFilters,
            hasExplicitFilters,
            $"{Request.Path}{Request.QueryString}",
            cancellationToken);
        SavedViewBar = resolved.Bar;
        if (!hasExplicitFilters)
        {
            Year = ParseInt(resolved.Filters, "Year");
            ParentCaseNumber = Value(resolved.Filters, "ParentCaseNumber");
            ProjectNumber = Value(resolved.Filters, "ProjectNumber");
            ProjectName = Value(resolved.Filters, "Name");
            PersonnelUserId = Value(resolved.Filters, "PersonnelUserId");
            StatusId = ParseInt(resolved.Filters, "StatusId");
            ProjectType = Enum.TryParse<ProjectType>(Value(resolved.Filters, "ProjectType"), out var projectType) ? projectType : null;
            OpenOnly = bool.TryParse(Value(resolved.Filters, "OpenOnly"), out var openOnly) && openOnly;
            AnalysisType = Value(resolved.Filters, "AnalysisType");
        }
    }

    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToPage();

    private static string? Value(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static int? ParseInt(IReadOnlyDictionary<string, string?> values, string key) =>
        int.TryParse(Value(values, key), out var value) ? value : null;

    private ProjectFilter CreateFilter()
    {
        return new ProjectFilter(
            Year,
            ParentCaseNumber,
            ProjectNumber,
            ProjectName,
            PersonnelUserId,
            StatusId,
            OpenOnly,
            AnalysisType,
            ProjectType);
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

        UserOptions.Insert(0, new SelectListItem("全部", ""));
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
            new MetricInsight("可見專案", TotalCount.ToString("N0"), "目前帳號可查看"),
            new MetricInsight("未結案", openCount.ToString("N0"), "仍在推進"),
            new MetricInsight("專案金額", totalAmount.ToString("N2"), "可见範圍彙總", "success"),
            new MetricInsight("平均進度", $"{averageProgress:0.#}%", "可见範圍均值", "info")
        ];

        var statusRows = await query
            .GroupBy(x => x.Status == null ? "未設定" : x.Status.Name)
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

        if (filter.ProjectType is not null)
        {
            query = query.Where(x => x.ProjectType == filter.ProjectType);
        }

        if (filter.OpenOnly)
        {
            query = query.Where(x => x.Status != null && !x.Status.IsClosed);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var upcomingUntil = today.AddDays(14);
        var staleBefore = DateTimeOffset.UtcNow.AddDays(-30);
        query = filter.AnalysisType switch
        {
            ProjectAnalysisTypes.Overdue => query.Where(x => x.Status != null && !x.Status.IsClosed && x.GanttPlan != null && x.GanttPlan.FinishDate < today && x.ProgressPercent < 100),
            ProjectAnalysisTypes.Pending => query.Where(x => x.Status != null && !x.Status.IsClosed && (x.Status.Code.ToLower().Contains("wait") || x.Status.Code.ToLower().Contains("block") || x.Status.Name.Contains("等待") || x.Status.Name.Contains("待處理") || x.Status.Name.Contains("阻塞") || x.CollectionPercent + 25 < x.ProgressPercent)),
            ProjectAnalysisTypes.Upcoming => query.Where(x => x.Status != null && !x.Status.IsClosed && x.GanttPlan != null && x.GanttPlan.Tasks.Any(task => task.ProgressPercent < 100 && task.PlannedFinishDate >= today && task.PlannedFinishDate <= upcomingUntil)),
            ProjectAnalysisTypes.StaleUpdate => query.Where(x => x.Status != null && !x.Status.IsClosed && x.UpdatedAt < staleBefore),
            _ => query
        };

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
            [nameof(ProjectType)] = ProjectType.HasValue ? ((int)ProjectType.Value).ToString() : null,
            [nameof(OpenOnly)] = OpenOnly.ToString(),
            [nameof(AnalysisType)] = AnalysisType,
            [nameof(ViewPreset)] = ViewPreset,
            [nameof(SavedViewId)] = SavedViewId?.ToString()
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
            items.Add(new FilterSummaryItem("母檔案號", ParentCaseNumber));
        }

        if (!string.IsNullOrWhiteSpace(ProjectNumber))
        {
            items.Add(new FilterSummaryItem("專案工號", ProjectNumber));
        }

        if (!string.IsNullOrWhiteSpace(ProjectName))
        {
            items.Add(new FilterSummaryItem("專案名稱", ProjectName));
        }

        if (!string.IsNullOrWhiteSpace(PersonnelUserId))
        {
            var userText = UserOptions.FirstOrDefault(x => x.Value == PersonnelUserId)?.Text ?? PersonnelUserId;
            items.Add(new FilterSummaryItem("專案人員", userText));
        }

        if (StatusId is not null)
        {
            var statusText = StatusOptions.FirstOrDefault(x => x.Value == StatusId.Value.ToString())?.Text ?? StatusId.Value.ToString();
            items.Add(new FilterSummaryItem("狀態", statusText));
        }

        if (ProjectType is not null)
        {
            var projectTypeText = ProjectTypeOptions.FirstOrDefault(x => x.Value == ((int)ProjectType.Value).ToString())?.Text ?? ProjectType.Value.ToString();
            items.Add(new FilterSummaryItem("專案類型", projectTypeText));
        }

        if (OpenOnly)
        {
            items.Add(new FilterSummaryItem("範圍", "未結案"));
        }

        if (!string.IsNullOrWhiteSpace(AnalysisType))
        {
            items.Add(new FilterSummaryItem("工作佇列", DisplayAnalysisType(AnalysisType)));
        }

        return items;
    }

    private static string DisplayAnalysisType(string analysisType) => analysisType switch
    {
        ProjectAnalysisTypes.Overdue => "我的逾期",
        ProjectAnalysisTypes.Pending => "待處理",
        ProjectAnalysisTypes.Upcoming => "近期節點",
        ProjectAnalysisTypes.StaleUpdate => "長期未更新",
        _ => analysisType
    };
}
