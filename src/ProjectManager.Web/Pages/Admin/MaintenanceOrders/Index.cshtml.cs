using Microsoft.AspNetCore.Authorization;
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
using System.Security.Claims;

namespace ProjectManager.Web.Pages.Admin.MaintenanceOrders;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class IndexModel(
    MaintenanceOrderService service,
    ApplicationDbContext db,
    SavedDataViewPageSupport savedDataViews) : PageModel
{
    private const string DataViewPageKey = "maintenance-orders";

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = PageSizeOptions.DefaultPageSize;

    [BindProperty(SupportsGet = true)]
    public int? Year { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? CustomerName { get; set; }

    [BindProperty(SupportsGet = true)]
    public MaintenanceMethod? Method { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ExecutorUserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public decimal? MinProgressPercent { get; set; }

    [BindProperty(SupportsGet = true)]
    public decimal? MaxProgressPercent { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ViewPreset { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? SavedViewId { get; set; }

    [BindProperty]
    public SaveDataViewInput Input { get; set; } = new();

    public SavedDataViewBarViewModel SavedViewBar { get; private set; } = null!;

    public IReadOnlyList<MaintenanceOrder> Orders { get; private set; } = [];

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public IReadOnlyList<MetricInsight> Metrics { get; private set; } = [];

    public IReadOnlyList<ChartSlice> MethodSlices { get; private set; } = [];

    public IReadOnlyList<ChartSlice> ProgressSlices { get; private set; } = [];

    public List<SelectListItem> ExecutorOptions { get; private set; } = [];

    public List<SelectListItem> MethodOptions { get; } =
    [
        new("现场保養", MaintenanceMethod.OnSite.ToString()),
        new("远程保養", MaintenanceMethod.Remote.ToString()),
        new("均有", MaintenanceMethod.Both.ToString())
    ];

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
        await ResolveSavedViewAsync(cancellationToken);
        await LoadOptionsAsync(cancellationToken);
        var page = await service.GetOrdersPageAsync(CreateFilter(), PageNumber, PageSize, cancellationToken);
        Orders = page.Items;
        TotalCount = page.TotalCount;
        PageNumber = page.PageNumber;
        PageSize = page.PageSize;
        TotalPages = page.TotalPages;
        await LoadInsightsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostSaveViewAsync(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
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
        var deleted = await savedDataViews.DeleteAsync(CurrentUserId(), id, cancellationToken);
        TempData[deleted ? "SuccessMessage" : "ErrorMessage"] = deleted ? "個人檢視已刪除。" : "找不到可刪除的個人檢視。";
        return RedirectToLocal(returnUrl);
    }

    public async Task<IActionResult> OnPostSetDefaultViewAsync(int id, string returnUrl, CancellationToken cancellationToken)
    {
        var updated = await savedDataViews.SetDefaultAsync(CurrentUserId(), id, cancellationToken);
        TempData[updated ? "SuccessMessage" : "ErrorMessage"] = updated ? "預設檢視已更新。" : "找不到可設定的個人檢視。";
        return RedirectToLocal(returnUrl);
    }

    private async Task ResolveSavedViewAsync(CancellationToken cancellationToken)
    {
        var explicitFilters = new Dictionary<string, string?>
        {
            ["Year"] = Year?.ToString(),
            ["CustomerName"] = CustomerName,
            ["Method"] = Method?.ToString(),
            ["ExecutorUserId"] = ExecutorUserId,
            ["MinProgressPercent"] = MinProgressPercent?.ToString(),
            ["MaxProgressPercent"] = MaxProgressPercent?.ToString()
        };
        var filterKeys = explicitFilters.Keys.ToArray();
        var hasExplicitFilters = filterKeys.Any(key => Request.Query.ContainsKey(key));
        var resolved = await savedDataViews.ResolveAsync(
            CurrentUserId(),
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
            CustomerName = Value(resolved.Filters, "CustomerName");
            Method = Enum.TryParse<MaintenanceMethod>(Value(resolved.Filters, "Method"), out var method) ? method : null;
            ExecutorUserId = Value(resolved.Filters, "ExecutorUserId");
            MinProgressPercent = ParseDecimal(resolved.Filters, "MinProgressPercent");
            MaxProgressPercent = ParseDecimal(resolved.Filters, "MaxProgressPercent");
        }
    }

    private string CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("找不到目前使用者。");

    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToPage();

    private static string? Value(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static int? ParseInt(IReadOnlyDictionary<string, string?> values, string key) =>
        int.TryParse(Value(values, key), out var value) ? value : null;

    private static decimal? ParseDecimal(IReadOnlyDictionary<string, string?> values, string key) =>
        decimal.TryParse(Value(values, key), out var value) ? value : null;

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return RedirectToPage("./Index", BuildRouteValuesWithPaging());
    }

    public async Task<IActionResult> OnPostBatchDeleteAsync(int[] ids, CancellationToken cancellationToken)
    {
        var attempted = ids.Distinct().Count();
        var succeeded = await service.DeleteManyAsync(ids, cancellationToken);
        TempData["SuccessMessage"] = $"已處理 {attempted} 筆：成功 {succeeded} 筆，失敗 {attempted - succeeded} 筆。";
        return RedirectToPage("./Index", BuildRouteValuesWithPaging());
    }

    private MaintenanceOrderFilter CreateFilter()
    {
        return new MaintenanceOrderFilter(
            Year,
            CustomerName,
            Method,
            ExecutorUserId,
            MinProgressPercent,
            MaxProgressPercent);
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        ExecutorOptions = await db.Users
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserName)
            .Select(x => new SelectListItem(
                string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? x.Id : x.DisplayName,
                x.Id))
            .ToListAsync(cancellationToken);
    }

    private async Task LoadInsightsAsync(CancellationToken cancellationToken)
    {
        var query = ApplyFilter(db.MaintenanceOrders
            .AsNoTracking()
            .Where(x => !x.IsDeleted));
        var completedCount = await query.CountAsync(x => x.ProgressPercent >= 100, cancellationToken);
        var averageProgress = await query.AverageAsync(x => (decimal?)x.ProgressPercent, cancellationToken) ?? 0;
        var executorCount = await query
            .Where(x => x.ExecutorUserId != null)
            .Select(x => x.ExecutorUserId)
            .Distinct()
            .CountAsync(cancellationToken);

        Metrics =
        [
            new MetricInsight("保養訂單", TotalCount.ToString("N0"), "当前有效訂單"),
            new MetricInsight("已完成", completedCount.ToString("N0"), "保養進度 100%"),
            new MetricInsight("平均進度", $"{averageProgress:0.#}%", "整体保養狀態", "info"),
            new MetricInsight("执行人数", executorCount.ToString("N0"), "已分配执行人", "success")
        ];

        var methodRows = await query
            .GroupBy(x => x.MaintenanceMethod)
            .Select(x => new { Label = x.Key, Value = x.Count() })
            .ToListAsync(cancellationToken);
        MethodSlices = ChartPalette.BuildSlices(methodRows.Select(x => (MethodLabel(x.Label), (decimal)x.Value)));

        var progressRows = new[]
        {
            ("0-49%", await query.CountAsync(x => x.ProgressPercent < 50, cancellationToken)),
            ("50-79%", await query.CountAsync(x => x.ProgressPercent >= 50 && x.ProgressPercent < 80, cancellationToken)),
            ("80-99%", await query.CountAsync(x => x.ProgressPercent >= 80 && x.ProgressPercent < 100, cancellationToken)),
            ("100%", await query.CountAsync(x => x.ProgressPercent >= 100, cancellationToken))
        };
        ProgressSlices = ChartPalette.BuildSlices(progressRows.Select(x => (x.Item1, (decimal)x.Item2)));
    }

    private static string MethodLabel(MaintenanceMethod method)
    {
        return method switch
        {
            MaintenanceMethod.OnSite => "现场保養",
            MaintenanceMethod.Remote => "远程保養",
            MaintenanceMethod.Both => "均有",
            _ => method.ToString()
        };
    }

    private IQueryable<MaintenanceOrder> ApplyFilter(IQueryable<MaintenanceOrder> query)
    {
        var filter = CreateFilter();
        if (filter.Year is not null)
        {
            query = query.Where(x => x.Year == filter.Year);
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
        {
            query = query.Where(x => x.CustomerName.Contains(filter.CustomerName));
        }

        if (filter.Method is not null)
        {
            query = query.Where(x => x.MaintenanceMethod == filter.Method);
        }

        if (!string.IsNullOrWhiteSpace(filter.ExecutorUserId))
        {
            query = query.Where(x => x.ExecutorUserId == filter.ExecutorUserId);
        }

        if (filter.MinProgressPercent is not null)
        {
            query = query.Where(x => x.ProgressPercent >= filter.MinProgressPercent);
        }

        if (filter.MaxProgressPercent is not null)
        {
            query = query.Where(x => x.ProgressPercent <= filter.MaxProgressPercent);
        }

        return query;
    }

    private Dictionary<string, string?> BuildRouteValues()
    {
        return new Dictionary<string, string?>
        {
            [nameof(Year)] = Year?.ToString(),
            [nameof(CustomerName)] = CustomerName,
            [nameof(Method)] = Method?.ToString(),
            [nameof(ExecutorUserId)] = ExecutorUserId,
            [nameof(MinProgressPercent)] = MinProgressPercent?.ToString(),
            [nameof(MaxProgressPercent)] = MaxProgressPercent?.ToString(),
            [nameof(ViewPreset)] = ViewPreset,
            [nameof(SavedViewId)] = SavedViewId?.ToString()
        };
    }

    private Dictionary<string, string?> BuildRouteValuesWithPaging()
    {
        var values = BuildRouteValues();
        values[nameof(PageNumber)] = PageNumber.ToString();
        values[nameof(PageSize)] = PageSize.ToString();
        return values;
    }

    private IReadOnlyList<FilterSummaryItem> BuildFilterSummaryItems()
    {
        var items = new List<FilterSummaryItem>();
        if (Year is not null)
        {
            items.Add(new FilterSummaryItem("年度", Year.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(CustomerName))
        {
            items.Add(new FilterSummaryItem("客戶名稱", CustomerName));
        }

        if (Method is not null)
        {
            items.Add(new FilterSummaryItem("保養方式", MethodLabel(Method.Value)));
        }

        if (!string.IsNullOrWhiteSpace(ExecutorUserId))
        {
            var userText = ExecutorOptions.FirstOrDefault(x => x.Value == ExecutorUserId)?.Text ?? ExecutorUserId;
            items.Add(new FilterSummaryItem("执行人", userText));
        }

        if (MinProgressPercent is not null || MaxProgressPercent is not null)
        {
            items.Add(new FilterSummaryItem("保養進度", $"{MinProgressPercent?.ToString() ?? "0"}%-{MaxProgressPercent?.ToString() ?? "100"}%"));
        }

        return items;
    }
}
