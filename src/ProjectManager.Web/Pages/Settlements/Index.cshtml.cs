using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Pages.Shared;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Settlements;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.Leader)]
public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Year { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Month { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = PageSizeOptions.DefaultPageSize;

    public IReadOnlyList<MonthlySettlementBatch> Batches { get; private set; } = [];

    public bool IsAdministrator => User.IsInRole(RoleNames.Administrator);

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public IReadOnlyList<MetricInsight> Metrics { get; private set; } = [];

    public IReadOnlyList<ChartSlice> MonthSlices { get; private set; } = [];

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
        var query = ApplyFilter(db.MonthlySettlementBatches
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
            .Include(x => x.Items)
            .AsQueryable());

        var page = await PagedResult<MonthlySettlementBatch>.CreateAsync(
            query
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
                .ThenByDescending(x => x.BatchNumber),
            PageNumber,
            PageSize,
            cancellationToken);
        Batches = page.Items;
        TotalCount = page.TotalCount;
        PageNumber = page.PageNumber;
        PageSize = page.PageSize;
        TotalPages = page.TotalPages;
        await LoadInsightsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        if (!User.IsInRole(RoleNames.Administrator))
        {
            return Forbid();
        }

        var batch = await db.MonthlySettlementBatches
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (batch is null)
        {
            return NotFound();
        }

        db.MonthlySettlementItems.RemoveRange(batch.Items);
        db.MonthlySettlementBatches.Remove(batch);
        await db.SaveChangesAsync(cancellationToken);

        return RedirectToPage("./Index", new { Year, Month, PageNumber, PageSize });
    }

    public async Task<IActionResult> OnPostBatchDeleteAsync(int[] ids, CancellationToken cancellationToken)
    {
        if (!User.IsInRole(RoleNames.Administrator))
        {
            return Forbid();
        }

        if (ids.Length > 0)
        {
            var idSet = ids.Distinct().ToArray();
            var batches = await db.MonthlySettlementBatches
                .Include(x => x.Items)
                .Where(x => idSet.Contains(x.Id))
                .ToListAsync(cancellationToken);

            db.MonthlySettlementItems.RemoveRange(batches.SelectMany(x => x.Items));
            db.MonthlySettlementBatches.RemoveRange(batches);
            await db.SaveChangesAsync(cancellationToken);
        }

        return RedirectToPage("./Index", new { Year, Month, PageNumber, PageSize });
    }

    private IQueryable<MonthlySettlementBatch> ApplyFilter(IQueryable<MonthlySettlementBatch> query)
    {
        if (Year is not null)
        {
            query = query.Where(x => x.Year == Year);
        }

        if (Month is not null)
        {
            query = query.Where(x => x.Month == Month);
        }

        return query;
    }

    private async Task LoadInsightsAsync(CancellationToken cancellationToken)
    {
        var query = ApplyFilter(db.MonthlySettlementBatches.AsNoTracking());
        var itemCount = await query.SelectMany(x => x.Items).CountAsync(cancellationToken);
        var latest = await query
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .Select(x => new { x.Year, x.Month })
            .FirstOrDefaultAsync(cancellationToken);

        Metrics =
        [
            new MetricInsight("月结批次", TotalCount.ToString("N0"), "当前筛选结果"),
            new MetricInsight("项目快照", itemCount.ToString("N0"), "已归档明细"),
            new MetricInsight("最新年月", latest is null ? "-" : $"{latest.Year}-{latest.Month:00}", "最近生成批次", "info")
        ];

        var rows = await query
            .GroupBy(x => new { x.Year, x.Month })
            .Select(x => new { x.Key.Year, x.Key.Month, Value = x.Count() })
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .Take(6)
            .ToListAsync(cancellationToken);
        MonthSlices = ChartPalette.BuildBars(
            rows
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .Select(x => ($"{x.Year}-{x.Month:00}", (decimal)x.Value)));
    }

    private Dictionary<string, string?> BuildRouteValues()
    {
        return new Dictionary<string, string?>
        {
            [nameof(Year)] = Year?.ToString(),
            [nameof(Month)] = Month?.ToString()
        };
    }

    private IReadOnlyList<FilterSummaryItem> BuildFilterSummaryItems()
    {
        var items = new List<FilterSummaryItem>();

        if (Year is not null)
        {
            items.Add(new FilterSummaryItem("年", Year.Value.ToString()));
        }

        if (Month is not null)
        {
            items.Add(new FilterSummaryItem("月", Month.Value.ToString()));
        }

        return items;
    }
}
