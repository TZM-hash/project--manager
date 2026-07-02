using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Pages.Shared;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.MaintenanceOrders;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class IndexModel(MaintenanceOrderService service, ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = PageSizeOptions.DefaultPageSize;

    public IReadOnlyList<MaintenanceOrder> Orders { get; private set; } = [];

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public IReadOnlyList<MetricInsight> Metrics { get; private set; } = [];

    public IReadOnlyList<ChartSlice> MethodSlices { get; private set; } = [];

    public IReadOnlyList<ChartSlice> HandoverSlices { get; private set; } = [];

    public PaginationViewModel Pagination => new(
        PageNumber,
        PageSize,
        TotalCount,
        TotalPages,
        "./Index",
        new Dictionary<string, string?>());

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var page = await service.GetOrdersPageAsync(PageNumber, PageSize, cancellationToken);
        Orders = page.Items;
        TotalCount = page.TotalCount;
        PageNumber = page.PageNumber;
        PageSize = page.PageSize;
        TotalPages = page.TotalPages;
        await LoadInsightsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return RedirectToPage("./Index", new { PageNumber, PageSize });
    }

    public async Task<IActionResult> OnPostBatchDeleteAsync(int[] ids, CancellationToken cancellationToken)
    {
        await service.DeleteManyAsync(ids, cancellationToken);
        return RedirectToPage("./Index", new { PageNumber, PageSize });
    }

    private async Task LoadInsightsAsync(CancellationToken cancellationToken)
    {
        var query = db.MaintenanceOrders
            .AsNoTracking()
            .Where(x => !x.IsDeleted);
        var completedCount = await query.CountAsync(x => x.HandoverPercent >= 100, cancellationToken);
        var averageHandover = await query.AverageAsync(x => (decimal?)x.HandoverPercent, cancellationToken) ?? 0;
        var executorCount = await query
            .Where(x => x.ExecutorUserId != null)
            .Select(x => x.ExecutorUserId)
            .Distinct()
            .CountAsync(cancellationToken);

        Metrics =
        [
            new MetricInsight("保养订单", TotalCount.ToString("N0"), "当前有效订单"),
            new MetricInsight("已移交完成", completedCount.ToString("N0"), "移交 100%"),
            new MetricInsight("平均移交", $"{averageHandover:0.#}%", "整体推进状态", "info"),
            new MetricInsight("执行人数", executorCount.ToString("N0"), "已分配执行人", "success")
        ];

        var methodRows = await query
            .GroupBy(x => x.MaintenanceMethod)
            .Select(x => new { Label = x.Key, Value = x.Count() })
            .ToListAsync(cancellationToken);
        MethodSlices = ChartPalette.BuildSlices(methodRows.Select(x => (MethodLabel(x.Label), (decimal)x.Value)));

        var handoverRows = new[]
        {
            ("0-49%", await query.CountAsync(x => x.HandoverPercent < 50, cancellationToken)),
            ("50-79%", await query.CountAsync(x => x.HandoverPercent >= 50 && x.HandoverPercent < 80, cancellationToken)),
            ("80-99%", await query.CountAsync(x => x.HandoverPercent >= 80 && x.HandoverPercent < 100, cancellationToken)),
            ("100%", await query.CountAsync(x => x.HandoverPercent >= 100, cancellationToken))
        };
        HandoverSlices = ChartPalette.BuildSlices(handoverRows.Select(x => (x.Item1, (decimal)x.Item2)));
    }

    private static string MethodLabel(MaintenanceMethod method)
    {
        return method switch
        {
            MaintenanceMethod.OnSite => "现场保养",
            MaintenanceMethod.Remote => "远程保养",
            MaintenanceMethod.Both => "均有",
            _ => method.ToString()
        };
    }
}
