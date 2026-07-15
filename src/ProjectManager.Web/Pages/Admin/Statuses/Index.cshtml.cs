using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Pages.Shared;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.Statuses;

[Authorize(Roles = RoleNames.BusinessManagerRoles)]
public sealed class IndexModel(
    ApplicationDbContext db,
    StatusMaintenanceService statusMaintenanceService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = PageSizeOptions.DefaultPageSize;

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? IsActive { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? IsClosed { get; set; }

    public IReadOnlyList<StatusListItem> Statuses { get; private set; } = [];

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public IReadOnlyList<MetricInsight> Metrics { get; private set; } = [];

    public IReadOnlyList<ChartSlice> ActiveSlices { get; private set; } = [];

    public IReadOnlyList<ChartSlice> ClosedSlices { get; private set; } = [];

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
        await LoadStatusesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        var result = await statusMaintenanceService.DeleteAsync(id, cancellationToken);
        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            await LoadStatusesAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage("./Index", BuildRouteValuesWithPaging());
    }

    public async Task<IActionResult> OnPostBatchDeleteAsync(int[] ids, CancellationToken cancellationToken)
    {
        foreach (var id in ids.Distinct())
        {
            var result = await statusMaintenanceService.DeleteAsync(id, cancellationToken);
            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadStatusesAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage("./Index", BuildRouteValuesWithPaging());
    }

    private async Task LoadStatusesAsync(CancellationToken cancellationToken)
    {
        var query = ApplyFilter(db.ProjectStatuses
            .AsNoTracking()
            .Include(x => x.Style))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new StatusListItem(
                x.Id,
                x.Code,
                x.Name,
                x.SortOrder,
                x.IsClosed,
                x.IsActive,
                x.Style == null ? "#1f2937" : x.Style.TextColor,
                x.Style == null ? "#e5e7eb" : x.Style.BackgroundColor,
                x.Style != null && x.Style.IsBold,
                db.Projects.Count(p => !p.IsDeleted && p.StatusId == x.Id)));
        var page = await PagedResult<StatusListItem>.CreateAsync(
            query,
            PageNumber,
            PageSize,
            cancellationToken);
        Statuses = page.Items;
        TotalCount = page.TotalCount;
        PageNumber = page.PageNumber;
        PageSize = page.PageSize;
        TotalPages = page.TotalPages;
        await LoadInsightsAsync(cancellationToken);
    }

    private async Task LoadInsightsAsync(CancellationToken cancellationToken)
    {
        var query = ApplyFilter(db.ProjectStatuses.AsNoTracking());
        var activeCount = await query.CountAsync(x => x.IsActive, cancellationToken);
        var inactiveCount = await query.CountAsync(x => !x.IsActive, cancellationToken);
        var closedCount = await query.CountAsync(x => x.IsClosed, cancellationToken);
        var openCount = await query.CountAsync(x => !x.IsClosed, cancellationToken);
        var unusedCount = await query.CountAsync(
            x => !db.Projects.Any(p => !p.IsDeleted && p.StatusId == x.Id),
            cancellationToken);

        Metrics =
        [
            new MetricInsight("狀態總數", TotalCount.ToString("N0"), "狀態設定项"),
            new MetricInsight("啟用狀態", activeCount.ToString("N0"), "前台可用"),
            new MetricInsight("未使用", unusedCount.ToString("N0"), "可刪除狀態", "info")
        ];

        ActiveSlices = ChartPalette.BuildSlices(
        [
            ("啟用", (decimal)activeCount),
            ("停用", (decimal)inactiveCount)
        ]);
        ClosedSlices = ChartPalette.BuildSlices(
        [
            ("未結案类", (decimal)openCount),
            ("结案类", (decimal)closedCount)
        ]);
    }

    private IQueryable<ProjectManager.Web.Models.ProjectStatus> ApplyFilter(
        IQueryable<ProjectManager.Web.Models.ProjectStatus> query)
    {
        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            query = query.Where(x => x.Code.Contains(Keyword) || x.Name.Contains(Keyword));
        }

        if (IsActive is not null)
        {
            query = query.Where(x => x.IsActive == IsActive);
        }

        if (IsClosed is not null)
        {
            query = query.Where(x => x.IsClosed == IsClosed);
        }

        return query;
    }

    private Dictionary<string, string?> BuildRouteValues()
    {
        return new Dictionary<string, string?>
        {
            [nameof(Keyword)] = Keyword,
            [nameof(IsActive)] = IsActive?.ToString(),
            [nameof(IsClosed)] = IsClosed?.ToString()
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
        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            items.Add(new FilterSummaryItem("關鍵字", Keyword));
        }

        if (IsActive is not null)
        {
            items.Add(new FilterSummaryItem("啟用", IsActive.Value ? "是" : "否"));
        }

        if (IsClosed is not null)
        {
            items.Add(new FilterSummaryItem("结案屬性", IsClosed.Value ? "结案类" : "未結案类"));
        }

        return items;
    }

    public sealed record StatusListItem(
        int Id,
        string Code,
        string Name,
        int SortOrder,
        bool IsClosed,
        bool IsActive,
        string TextColor,
        string BackgroundColor,
        bool IsBold,
        int ProjectCount);
}
