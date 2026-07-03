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

namespace ProjectManager.Web.Pages.Admin.Users;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class IndexModel(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : PageModel
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
    public bool? IsWeakManaged { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? RoleName { get; set; }

    public IList<UserListItem> Users { get; private set; } = [];

    /// <summary>当前登录用户是否为 admin 账号（唯一可管理密码的账号）。</summary>
    public bool CanManagePassword { get; private set; }

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public IReadOnlyList<MetricInsight> Metrics { get; private set; } = [];

    public IReadOnlyList<ChartSlice> ActiveSlices { get; private set; } = [];

    public IReadOnlyList<ChartSlice> RoleSlices { get; private set; } = [];

    public List<SelectListItem> RoleOptions { get; private set; } = [];

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
        await LoadRoleOptionsAsync(cancellationToken);
        var currentUserId = userManager.GetUserId(User);
        var currentUser = await userManager.FindByIdAsync(currentUserId ?? string.Empty);
        CanManagePassword = string.Equals(currentUser?.UserName, "admin", StringComparison.OrdinalIgnoreCase);

        var query = await ApplyFilterAsync(userManager.Users.AsNoTracking(), cancellationToken);
        var page = await PagedResult<ApplicationUser>.CreateAsync(
            query.OrderBy(x => x.UserName),
            PageNumber,
            PageSize,
            cancellationToken);

        var rows = new List<UserListItem>();
        foreach (var user in page.Items)
        {
            rows.Add(new UserListItem(
                user.Id,
                user.UserName ?? string.Empty,
                user.DisplayName,
                user.Email ?? string.Empty,
                user.IsActive,
                await userManager.GetRolesAsync(user)));
        }

        Users = rows;
        TotalCount = page.TotalCount;
        PageNumber = page.PageNumber;
        PageSize = page.PageSize;
        TotalPages = page.TotalPages;
        await LoadInsightsAsync(cancellationToken);
    }

    private async Task<IQueryable<ApplicationUser>> ApplyFilterAsync(
        IQueryable<ApplicationUser> query,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            query = query.Where(x =>
                (x.UserName != null && x.UserName.Contains(Keyword)) ||
                x.DisplayName.Contains(Keyword) ||
                (x.Email != null && x.Email.Contains(Keyword)));
        }

        if (IsActive is not null)
        {
            query = query.Where(x => x.IsActive == IsActive);
        }

        if (IsWeakManaged is not null)
        {
            query = query.Where(x => x.IsWeakManaged == IsWeakManaged);
        }

        if (!string.IsNullOrWhiteSpace(RoleName))
        {
            var roleId = await db.Roles
                .Where(x => x.Name == RoleName)
                .Select(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(roleId))
            {
                query = query.Where(_ => false);
            }
            else
            {
                var userIds = db.UserRoles
                    .Where(x => x.RoleId == roleId)
                    .Select(x => x.UserId);
                query = query.Where(x => userIds.Contains(x.Id));
            }
        }

        return query;
    }

    private async Task LoadRoleOptionsAsync(CancellationToken cancellationToken)
    {
        RoleOptions = await db.Roles
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem(RoleNames.GetDisplayName(x.Name ?? x.Id), x.Name ?? x.Id))
            .ToListAsync(cancellationToken);
    }

    private async Task LoadInsightsAsync(CancellationToken cancellationToken)
    {
        var filteredUsers = await ApplyFilterAsync(userManager.Users.AsNoTracking(), cancellationToken);
        var activeCount = await filteredUsers.CountAsync(x => x.IsActive, cancellationToken);
        var inactiveCount = await filteredUsers.CountAsync(x => !x.IsActive, cancellationToken);
        var weakManagedCount = await filteredUsers.CountAsync(x => x.IsWeakManaged, cancellationToken);

        Metrics =
        [
            new MetricInsight("用户总数", TotalCount.ToString("N0"), "系统账号"),
            new MetricInsight("启用账号", activeCount.ToString("N0"), "可参与业务"),
            new MetricInsight("弱管理账号", weakManagedCount.ToString("N0"), "轻量维护对象", "info")
        ];

        ActiveSlices = ChartPalette.BuildSlices(
        [
            ("启用", (decimal)activeCount),
            ("停用", (decimal)inactiveCount)
        ]);

        var roleRows = await db.UserRoles
            .AsNoTracking()
            .Join(
                db.Roles.AsNoTracking(),
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => role.Name ?? role.Id)
            .GroupBy(x => x)
            .Select(x => new { Label = x.Key, Value = x.Count() })
            .OrderByDescending(x => x.Value)
            .ToListAsync(cancellationToken);
        RoleSlices = ChartPalette.BuildSlices(
            roleRows.Select(x => (RoleNames.GetDisplayName(x.Label), (decimal)x.Value)));
    }

    private Dictionary<string, string?> BuildRouteValues()
    {
        return new Dictionary<string, string?>
        {
            [nameof(Keyword)] = Keyword,
            [nameof(IsActive)] = IsActive?.ToString(),
            [nameof(IsWeakManaged)] = IsWeakManaged?.ToString(),
            [nameof(RoleName)] = RoleName
        };
    }

    private IReadOnlyList<FilterSummaryItem> BuildFilterSummaryItems()
    {
        var items = new List<FilterSummaryItem>();
        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            items.Add(new FilterSummaryItem("关键字", Keyword));
        }

        if (IsActive is not null)
        {
            items.Add(new FilterSummaryItem("启用", IsActive.Value ? "是" : "否"));
        }

        if (IsWeakManaged is not null)
        {
            items.Add(new FilterSummaryItem("弱管理", IsWeakManaged.Value ? "是" : "否"));
        }

        if (!string.IsNullOrWhiteSpace(RoleName))
        {
            items.Add(new FilterSummaryItem("角色", RoleNames.GetDisplayName(RoleName)));
        }

        return items;
    }

    public sealed record UserListItem(
        string Id,
        string UserName,
        string DisplayName,
        string Email,
        bool IsActive,
        IList<string> Roles);
}
