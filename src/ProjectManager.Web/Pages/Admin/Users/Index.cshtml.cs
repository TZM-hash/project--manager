using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

    public IList<UserListItem> Users { get; private set; } = [];

    /// <summary>当前登录用户是否为 admin 账号（唯一可管理密码的账号）。</summary>
    public bool CanManagePassword { get; private set; }

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public IReadOnlyList<MetricInsight> Metrics { get; private set; } = [];

    public IReadOnlyList<ChartSlice> ActiveSlices { get; private set; } = [];

    public IReadOnlyList<ChartSlice> RoleSlices { get; private set; } = [];

    public PaginationViewModel Pagination => new(
        PageNumber,
        PageSize,
        TotalCount,
        TotalPages,
        "./Index",
        new Dictionary<string, string?>());

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var currentUserId = userManager.GetUserId(User);
        var currentUser = await userManager.FindByIdAsync(currentUserId ?? string.Empty);
        CanManagePassword = string.Equals(currentUser?.UserName, "admin", StringComparison.OrdinalIgnoreCase);

        var query = userManager.Users
            .AsNoTracking()
            .OrderBy(x => x.UserName);
        var page = await PagedResult<ApplicationUser>.CreateAsync(
            query,
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

    private async Task LoadInsightsAsync(CancellationToken cancellationToken)
    {
        var activeCount = await userManager.Users.CountAsync(x => x.IsActive, cancellationToken);
        var inactiveCount = await userManager.Users.CountAsync(x => !x.IsActive, cancellationToken);
        var weakManagedCount = await userManager.Users.CountAsync(x => x.IsWeakManaged, cancellationToken);

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

    public sealed record UserListItem(
        string Id,
        string UserName,
        string DisplayName,
        string Email,
        bool IsActive,
        IList<string> Roles);
}
