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

namespace ProjectManager.Web.Pages.Settlements;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.Leader)]
public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Year { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Month { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? BatchNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? CreatedByUserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Notes { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = PageSizeOptions.DefaultPageSize;

    public IReadOnlyList<MonthlySettlementBatch> Batches { get; private set; } = [];

    public bool IsAdministrator => User.CanManageAllBusinessData();

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public IReadOnlyList<MetricInsight> Metrics { get; private set; } = [];

    public IReadOnlyList<ChartSlice> MonthSlices { get; private set; } = [];

    public List<SelectListItem> CreatedByOptions { get; private set; } = [];

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
        await LoadOptionsAsync(cancellationToken);
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
        if (!User.CanManageAllBusinessData())
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

        return RedirectToPage("./Index", BuildRouteValuesWithPaging());
    }

    public async Task<IActionResult> OnPostBatchDeleteAsync(int[] ids, CancellationToken cancellationToken)
    {
        if (!User.CanManageAllBusinessData())
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

        return RedirectToPage("./Index", BuildRouteValuesWithPaging());
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        CreatedByOptions = await db.Users
            .AsNoTracking()
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserName)
            .Select(x => new SelectListItem(
                string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? x.Id : x.DisplayName,
                x.Id))
            .ToListAsync(cancellationToken);
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

        if (BatchNumber is not null)
        {
            query = query.Where(x => x.BatchNumber == BatchNumber);
        }

        if (!string.IsNullOrWhiteSpace(CreatedByUserId))
        {
            query = query.Where(x => x.CreatedByUserId == CreatedByUserId);
        }

        if (!string.IsNullOrWhiteSpace(Notes))
        {
            query = query.Where(x => x.Notes != null && x.Notes.Contains(Notes));
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
            new MetricInsight("月結批次", TotalCount.ToString("N0"), "当前篩選結果"),
            new MetricInsight("專案快照", itemCount.ToString("N0"), "已歸檔明細"),
            new MetricInsight("最新年月", latest is null ? "-" : $"{latest.Year}-{latest.Month:00}", "最近產生批次", "info")
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
            [nameof(Month)] = Month?.ToString(),
            [nameof(BatchNumber)] = BatchNumber?.ToString(),
            [nameof(CreatedByUserId)] = CreatedByUserId,
            [nameof(Notes)] = Notes
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
            items.Add(new FilterSummaryItem("年", Year.Value.ToString()));
        }

        if (Month is not null)
        {
            items.Add(new FilterSummaryItem("月", Month.Value.ToString()));
        }

        if (BatchNumber is not null)
        {
            items.Add(new FilterSummaryItem("批次", BatchNumber.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(CreatedByUserId))
        {
            var userText = CreatedByOptions.FirstOrDefault(x => x.Value == CreatedByUserId)?.Text ?? CreatedByUserId;
            items.Add(new FilterSummaryItem("建立人", userText));
        }

        if (!string.IsNullOrWhiteSpace(Notes))
        {
            items.Add(new FilterSummaryItem("備註", Notes));
        }

        return items;
    }
}
