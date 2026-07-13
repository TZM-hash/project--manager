using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    public int TotalProjectCount { get; private set; }

    public int OpenProjectCount { get; private set; }

    public int ActiveStatusCount { get; private set; }

    public int SettlementBatchCount { get; private set; }

    public int PlanningProjectCount { get; private set; }

    public int MaintenanceOrderCount { get; private set; }

    public decimal OpenProjectPercent { get; private set; }

    public IReadOnlyList<MetricInsight> Metrics { get; private set; } = [];

    public IReadOnlyList<ChartSlice> StatusSlices { get; private set; } = [];

    public IReadOnlyList<ChartSlice> OpenClosedSlices { get; private set; } = [];

    public IReadOnlyList<ChartSlice> SettlementSlices { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        TotalProjectCount = await db.Projects
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        OpenProjectCount = await db.Projects
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.Status != null && !x.Status.IsClosed, cancellationToken);

        ActiveStatusCount = await db.ProjectStatuses
            .AsNoTracking()
            .CountAsync(x => x.IsActive, cancellationToken);

        SettlementBatchCount = await db.MonthlySettlementBatches
            .AsNoTracking()
            .CountAsync(cancellationToken);

        PlanningProjectCount = await db.PlanningProjects
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        MaintenanceOrderCount = await db.MaintenanceOrders
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        OpenProjectPercent = TotalProjectCount == 0
            ? 0
            : Math.Round(OpenProjectCount / (decimal)TotalProjectCount * 100, 1);

        Metrics =
        [
            new MetricInsight("專案總數", TotalProjectCount.ToString("N0"), "当前有效專案"),
            new MetricInsight("未結案專案", OpenProjectCount.ToString("N0"), $"{OpenProjectPercent:0.#}% 仍在推進"),
            new MetricInsight("規劃中專案", PlanningProjectCount.ToString("N0"), "待正式立项", "info"),
            new MetricInsight("保養訂單", MaintenanceOrderCount.ToString("N0"), "年度保養跟踪", "success"),
            new MetricInsight("月結批次", SettlementBatchCount.ToString("N0"), "歷史快照")
        ];

        var statusRows = await db.Projects
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .GroupBy(x => x.Status == null ? "未設定" : x.Status.Name)
            .Select(x => new { Label = x.Key, Value = x.Count() })
            .OrderByDescending(x => x.Value)
            .ToListAsync(cancellationToken);
        StatusSlices = ChartPalette.BuildSlices(statusRows.Select(x => (x.Label, (decimal)x.Value)));

        OpenClosedSlices = ChartPalette.BuildSlices(
        [
            ("未結案", (decimal)OpenProjectCount),
            ("已结案", (decimal)Math.Max(0, TotalProjectCount - OpenProjectCount))
        ]);

        var settlementRows = await db.MonthlySettlementBatches
            .AsNoTracking()
            .GroupBy(x => new { x.Year, x.Month })
            .Select(x => new { x.Key.Year, x.Key.Month, Value = x.Count() })
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .Take(6)
            .ToListAsync(cancellationToken);
        SettlementSlices = ChartPalette.BuildBars(
            settlementRows
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .Select(x => ($"{x.Year}-{x.Month:00}", (decimal)x.Value)));
    }
}
