using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Pages.Shared;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.Statuses;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class IndexModel(
    ApplicationDbContext db,
    StatusMaintenanceService statusMaintenanceService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = PageSizeOptions.DefaultPageSize;

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
        new Dictionary<string, string?>());

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

        return RedirectToPage("./Index", new { PageNumber, PageSize });
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

        return RedirectToPage("./Index", new { PageNumber, PageSize });
    }

    private async Task LoadStatusesAsync(CancellationToken cancellationToken)
    {
        var query = db.ProjectStatuses
            .AsNoTracking()
            .Include(x => x.Style)
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
        var query = db.ProjectStatuses.AsNoTracking();
        var activeCount = await query.CountAsync(x => x.IsActive, cancellationToken);
        var inactiveCount = await query.CountAsync(x => !x.IsActive, cancellationToken);
        var closedCount = await query.CountAsync(x => x.IsClosed, cancellationToken);
        var openCount = await query.CountAsync(x => !x.IsClosed, cancellationToken);
        var unusedCount = await query.CountAsync(
            x => !db.Projects.Any(p => !p.IsDeleted && p.StatusId == x.Id),
            cancellationToken);

        Metrics =
        [
            new MetricInsight("状态总数", TotalCount.ToString("N0"), "状态配置项"),
            new MetricInsight("启用状态", activeCount.ToString("N0"), "前台可用"),
            new MetricInsight("未使用", unusedCount.ToString("N0"), "可删除状态", "info")
        ];

        ActiveSlices = ChartPalette.BuildSlices(
        [
            ("启用", (decimal)activeCount),
            ("停用", (decimal)inactiveCount)
        ]);
        ClosedSlices = ChartPalette.BuildSlices(
        [
            ("未结案类", (decimal)openCount),
            ("结案类", (decimal)closedCount)
        ]);
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
