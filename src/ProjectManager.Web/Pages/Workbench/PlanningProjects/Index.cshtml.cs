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

namespace ProjectManager.Web.Pages.Workbench.PlanningProjects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader + "," + RoleNames.Viewer)]
public sealed class IndexModel(
    PlanningProjectService planningProjectService,
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = PageSizeOptions.DefaultPageSize;

    [BindProperty(SupportsGet = true)]
    public string? Name { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? LeaderUserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Vendor { get; set; }

    public IReadOnlyList<PlanningProject> Projects { get; private set; } = [];

    public Dictionary<string, string> UserDisplayNames { get; private set; } = [];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public IReadOnlyList<MetricInsight> Metrics { get; private set; } = [];

    public IReadOnlyList<ChartSlice> LeaderSlices { get; private set; } = [];

    public IReadOnlyList<ChartSlice> VendorSlices { get; private set; } = [];

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
        await LoadUserDisplayNamesAsync(cancellationToken);
        var page = await planningProjectService.GetPlanningProjectsPageAsync(
            new PlanningProjectFilter(Name, LeaderUserId, Vendor),
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

    public string GetLeaderDisplayNames(string? leaderUserId)
    {
        if (string.IsNullOrWhiteSpace(leaderUserId))
        {
            return "-";
        }

        var ids = leaderUserId.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var names = ids
            .Where(id => UserDisplayNames.TryGetValue(id, out _))
            .Select(id => UserDisplayNames[id])
            .ToList();
        return names.Count > 0 ? string.Join("、", names) : "-";
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        await planningProjectService.DeleteAsync(id, cancellationToken);
        return RedirectToPage("./Index", BuildRouteValuesWithPaging());
    }

    public async Task<IActionResult> OnPostBatchDeleteAsync(int[] ids, CancellationToken cancellationToken)
    {
        await planningProjectService.DeleteManyAsync(ids, cancellationToken);
        return RedirectToPage("./Index", BuildRouteValuesWithPaging());
    }

    public IActionResult OnPostBatchPrint(int[] ids)
    {
        if (ids == null || ids.Length == 0)
        {
            return RedirectToPage("./Index");
        }

        return RedirectToPage("./PrintList", new { ids = string.Join(",", ids) });
    }

    private async Task LoadUserDisplayNamesAsync(CancellationToken cancellationToken)
    {
        // 负责人字段可能保存单人，也可能是逗号分隔的多人 ID，这里统一展开为显示名。
        var users = await userManager.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        UserDisplayNames = users.ToDictionary(
            x => x.Id,
            x => string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? x.Id : x.DisplayName);
        UserOptions = users
            .Where(x => x.IsActive)
            .Select(x => new SelectListItem(
                string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? x.Id : x.DisplayName,
                x.Id))
            .OrderBy(x => x.Text)
            .ToList();
    }

    private async Task LoadInsightsAsync(CancellationToken cancellationToken)
    {
        var projects = await ApplyFilter(db.PlanningProjects
            .AsNoTracking()
            .Where(x => !x.IsDeleted))
            .Select(x => new
            {
                x.LeaderUserId,
                x.Vendor,
                x.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        var recentCount = projects.Count(x => x.UpdatedAt >= DateTimeOffset.UtcNow.AddDays(-30));
        var vendorCount = projects
            .Select(x => string.IsNullOrWhiteSpace(x.Vendor) ? "未填写" : x.Vendor.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Metrics =
        [
            new MetricInsight("规划中专案", TotalCount.ToString("N0"), "尚未正式立项"),
            new MetricInsight("近 30 天更新", recentCount.ToString("N0"), "有最新动态"),
            new MetricInsight("厂商数", vendorCount.ToString("N0"), "当前资料覆盖", "success")
        ];

        var leaderRows = projects
            .SelectMany(x => SplitLeaderIds(x.LeaderUserId))
            .GroupBy(x => ResolveLeaderName(x))
            .Select(x => (x.Key, (decimal)x.Count()))
            .OrderByDescending(x => x.Item2);
        LeaderSlices = ChartPalette.BuildSlices(leaderRows);

        var vendorRows = projects
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Vendor) ? "未填写" : x.Vendor.Trim())
            .Select(x => (x.Key, (decimal)x.Count()))
            .OrderByDescending(x => x.Item2);
        VendorSlices = ChartPalette.BuildSlices(vendorRows);
    }

    private static IEnumerable<string> SplitLeaderIds(string? leaderUserId)
    {
        if (string.IsNullOrWhiteSpace(leaderUserId))
        {
            yield return "未指定";
            yield break;
        }

        foreach (var id in leaderUserId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return id;
        }
    }

    private string ResolveLeaderName(string id)
    {
        return UserDisplayNames.TryGetValue(id, out var name) ? name : id;
    }

    private IQueryable<PlanningProject> ApplyFilter(IQueryable<PlanningProject> query)
    {
        if (!string.IsNullOrWhiteSpace(Name))
        {
            query = query.Where(x => x.Name.Contains(Name));
        }

        if (!string.IsNullOrWhiteSpace(LeaderUserId))
        {
            query = query.Where(x => x.LeaderUserId != null && x.LeaderUserId.Contains(LeaderUserId));
        }

        if (!string.IsNullOrWhiteSpace(Vendor))
        {
            query = query.Where(x => x.Vendor != null && x.Vendor.Contains(Vendor));
        }

        return query;
    }

    private Dictionary<string, string?> BuildRouteValues()
    {
        return new Dictionary<string, string?>
        {
            [nameof(Name)] = Name,
            [nameof(LeaderUserId)] = LeaderUserId,
            [nameof(Vendor)] = Vendor
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
        if (!string.IsNullOrWhiteSpace(Name))
        {
            items.Add(new FilterSummaryItem("项目名称", Name));
        }

        if (!string.IsNullOrWhiteSpace(LeaderUserId))
        {
            var userText = UserOptions.FirstOrDefault(x => x.Value == LeaderUserId)?.Text ?? LeaderUserId;
            items.Add(new FilterSummaryItem("项目负责人", userText));
        }

        if (!string.IsNullOrWhiteSpace(Vendor))
        {
            items.Add(new FilterSummaryItem("厂商", Vendor));
        }

        return items;
    }
}
