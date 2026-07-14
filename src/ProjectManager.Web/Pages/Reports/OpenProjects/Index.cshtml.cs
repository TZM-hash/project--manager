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
using ProjectManager.Web.Services.DataViews;

namespace ProjectManager.Web.Pages.Reports.OpenProjects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.Leader + "," + RoleNames.Viewer + "," + RoleNames.ProjectStaff)]
public sealed class IndexModel(
    ProjectQueryService projectQueryService,
    ExcelReportService excelReportService,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    SavedDataViewPageSupport savedDataViews) : PageModel
{
    private const string DataViewPageKey = "open-project-report";

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
    public string? AnalysisType { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = PageSizeOptions.DefaultPageSize;

    [BindProperty(SupportsGet = true)]
    public string? ViewPreset { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? SavedViewId { get; set; }

    [BindProperty]
    public SaveDataViewInput Input { get; set; } = new();

    public SavedDataViewBarViewModel SavedViewBar { get; private set; } = null!;

    public IReadOnlyList<Project> Projects { get; private set; } = [];

    public List<SelectListItem> StatusOptions { get; private set; } = [];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public IReadOnlyList<MetricInsight> Metrics { get; private set; } = [];

    public IReadOnlyList<ChartSlice> StatusSlices { get; private set; } = [];

    public IReadOnlyList<ChartSlice> ProgressSlices { get; private set; } = [];

    public IReadOnlyList<MetricInsight> AnalysisCards { get; private set; } = [];

    public FilterSummaryViewModel FilterSummary => new(
        "./Index",
        BuildFilterSummaryItems(),
        new Dictionary<string, string?> { [nameof(PageSize)] = PageSize.ToString() });

    public PaginationViewModel Pagination => new(
        PageNumber,
        PageSize,
        TotalCount,
        TotalPages,
        "./Index",
        BuildRouteValues());

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await ResolveSavedViewAsync(cancellationToken);
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
            OpenOnly: true,
            NormalizeAnalysisType(AnalysisType));
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

        UserOptions.Insert(0, new SelectListItem("全部", ""));
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
            ["AnalysisType"] = AnalysisType
        };
        var filterKeys = new[] { "Year", "ParentCaseNumber", "ProjectNumber", "ProjectName", "Name", "PersonnelUserId", "StatusId", "AnalysisType" };
        var hasExplicitFilters = filterKeys.Any(key => Request.Query.ContainsKey(key));
        var userId = userManager.GetUserId(User) ?? throw new InvalidOperationException("找不到目前使用者。");
        var resolved = await savedDataViews.ResolveAsync(
            userId,
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
            ApplySavedFilters(resolved.Filters);
        }
    }

    private void ApplySavedFilters(IReadOnlyDictionary<string, string?> filters)
    {
        Year = ParseInt(filters, "Year");
        ParentCaseNumber = Value(filters, "ParentCaseNumber");
        ProjectNumber = Value(filters, "ProjectNumber");
        ProjectName = Value(filters, "Name");
        PersonnelUserId = Value(filters, "PersonnelUserId");
        StatusId = ParseInt(filters, "StatusId");
        AnalysisType = Value(filters, "AnalysisType");
    }

    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToPage();

    private static string? Value(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static int? ParseInt(IReadOnlyDictionary<string, string?> values, string key) =>
        int.TryParse(Value(values, key), out var value) ? value : null;

    private async Task LoadInsightsAsync(CancellationToken cancellationToken)
    {
        var query = ApplyFilter(db.Projects.AsNoTracking().Where(x => !x.IsDeleted));
        var totalAmount = await query.SumAsync(x => (decimal?)x.ProjectAmount, cancellationToken) ?? 0;
        var purchaseAmount = await query
            .SelectMany(x => x.PurchaseRequests.Where(p => !p.IsDeleted))
            .SumAsync(x => (decimal?)x.PurchaseAmount, cancellationToken) ?? 0;
        var averageProgress = await query.AverageAsync(x => (decimal?)x.ProgressPercent, cancellationToken) ?? 0;
        var lowProgressCount = await query.CountAsync(x => x.ProgressPercent < 30, cancellationToken);
        var collectionLagCount = await query.CountAsync(x => x.CollectionPercent + 25 < x.ProgressPercent, cancellationToken);
        var staleDate = DateTimeOffset.UtcNow.AddDays(-30);
        var updatedAtValues = await query
            .Select(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);
        var staleCount = updatedAtValues.Count(x => x < staleDate);
        var amountRows = await query
            .Select(x => new { x.ProjectNumber, x.Name, x.ProjectAmount })
            .ToListAsync(cancellationToken);
        var topAmountProject = amountRows
            .OrderByDescending(x => x.ProjectAmount)
            .FirstOrDefault();

        Metrics =
        [
            new MetricInsight("未結案專案", TotalCount.ToString("N0"), "当前篩選結果"),
            new MetricInsight("專案金額", totalAmount.ToString("N2"), "未結案彙總", "success"),
            new MetricInsight("请购金額", purchaseAmount.ToString("N2"), "未結案请购彙總", "info"),
            new MetricInsight("平均進度", $"{averageProgress:0.#}%", "目前篩選均值")
        ];

        AnalysisCards =
        [
            new MetricInsight(
                "需关注專案",
                lowProgressCount.ToString("N0"),
                "進度低于 30%",
                "danger",
                BuildAnalysisRouteValues(ProjectAnalysisTypes.LowProgress)),
            new MetricInsight(
                "回款滞后",
                collectionLagCount.ToString("N0"),
                "收款比例落后進度 25% 以上",
                "warning",
                BuildAnalysisRouteValues(ProjectAnalysisTypes.CollectionLag)),
            new MetricInsight(
                "久未更新",
                staleCount.ToString("N0"),
                "超过 30 天未更新",
                "info",
                BuildAnalysisRouteValues(ProjectAnalysisTypes.StaleUpdate)),
            new MetricInsight(
                "最高金額專案",
                topAmountProject is null ? "-" : topAmountProject.ProjectAmount.ToString("N2"),
                topAmountProject is null ? "暂无專案" : $"{topAmountProject.ProjectNumber} / {topAmountProject.Name}",
                "success",
                topAmountProject is null ? null : BuildTopAmountRouteValues(topAmountProject.ProjectNumber))
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

        query = query.Where(x => x.Status != null && !x.Status.IsClosed);

        query = filter.AnalysisType switch
        {
            ProjectAnalysisTypes.LowProgress => query.Where(x => x.ProgressPercent < 30),
            ProjectAnalysisTypes.CollectionLag => query.Where(x => x.CollectionPercent + 25 < x.ProgressPercent),
            ProjectAnalysisTypes.StaleUpdate => query.Where(x => x.UpdatedAt < DateTimeOffset.UtcNow.AddDays(-30)),
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
            [nameof(AnalysisType)] = NormalizeAnalysisType(AnalysisType)
            , [nameof(ViewPreset)] = ViewPreset
            , [nameof(SavedViewId)] = SavedViewId?.ToString()
        };
    }

    private Dictionary<string, string?> BuildAnalysisRouteValues(string analysisType)
    {
        var routeValues = BuildRouteValues();
        routeValues[nameof(PageNumber)] = "1";
        routeValues[nameof(PageSize)] = PageSize.ToString();
        routeValues[nameof(AnalysisType)] = analysisType;
        return routeValues;
    }

    private Dictionary<string, string?> BuildTopAmountRouteValues(string projectNumber)
    {
        var routeValues = BuildRouteValues();
        routeValues[nameof(PageNumber)] = "1";
        routeValues[nameof(PageSize)] = PageSize.ToString();
        routeValues[nameof(ProjectNumber)] = projectNumber;
        routeValues[nameof(AnalysisType)] = null;
        return routeValues;
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
            items.Add(new FilterSummaryItem("母案案號", ParentCaseNumber));
        }

        if (!string.IsNullOrWhiteSpace(ProjectNumber))
        {
            items.Add(new FilterSummaryItem("專案工號", ProjectNumber));
        }

        if (!string.IsNullOrWhiteSpace(ProjectName))
        {
            items.Add(new FilterSummaryItem("專案名稱", ProjectName));
        }

        if (!string.IsNullOrWhiteSpace(PersonnelUserId) && !User.IsInRole(RoleNames.ProjectStaff))
        {
            var userText = UserOptions.FirstOrDefault(x => x.Value == PersonnelUserId)?.Text ?? PersonnelUserId;
            items.Add(new FilterSummaryItem("專案人員", userText));
        }

        if (StatusId is not null)
        {
            var statusText = StatusOptions.FirstOrDefault(x => x.Value == StatusId.Value.ToString())?.Text ?? StatusId.Value.ToString();
            items.Add(new FilterSummaryItem("狀態", statusText));
        }

        var analysisText = DisplayAnalysisType(NormalizeAnalysisType(AnalysisType));
        if (!string.IsNullOrWhiteSpace(analysisText))
        {
            items.Add(new FilterSummaryItem("分析钻取", analysisText));
        }

        return items;
    }

    private static string? NormalizeAnalysisType(string? analysisType)
    {
        return analysisType is ProjectAnalysisTypes.LowProgress or ProjectAnalysisTypes.CollectionLag or ProjectAnalysisTypes.StaleUpdate
            ? analysisType
            : null;
    }

    private static string? DisplayAnalysisType(string? analysisType)
    {
        return analysisType switch
        {
            ProjectAnalysisTypes.LowProgress => "需关注專案",
            ProjectAnalysisTypes.CollectionLag => "回款滞后",
            ProjectAnalysisTypes.StaleUpdate => "久未更新",
            _ => null
        };
    }
}
