using System.Data;
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

    /// <summary>当前登入使用者是否为 admin 帳號（唯一可管理密碼的帳號）。</summary>
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

    public async Task<IActionResult> OnPostDeleteAsync(string id, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var blockReason = await GetDeleteBlockReasonAsync(user);
        if (blockReason is not null)
        {
            ModelState.AddModelError(string.Empty, blockReason);
            await OnGetAsync(cancellationToken);
            return Page();
        }

        IdentityResult result;
        try
        {
            result = await userManager.DeleteAsync(user);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            ModelState.AddModelError(string.Empty, "此使用者已被專案或歷史資料引用，無法刪除；如不再使用，請改為停用帳號。");
            await OnGetAsync(cancellationToken);
            return Page();
        }
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            await OnGetAsync(cancellationToken);
            return Page();
        }

        await transaction.CommitAsync(cancellationToken);
        return RedirectToPage("./Index", BuildRouteValues());
    }

    public async Task<IActionResult> OnPostBatchDeleteAsync(string[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return RedirectToPage("./Index", BuildRouteValues());
        }

        var users = new List<ApplicationUser>();
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            var user = await userManager.FindByIdAsync(id);
            if (user != null)
            {
                users.Add(user);
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var currentUserId = userManager.GetUserId(User);
        if (users.Any(user => string.Equals(user.Id, currentUserId, StringComparison.Ordinal)))
        {
            ModelState.AddModelError(string.Empty, "目前登入中的帳號不能刪除。");
        }

        var administrators = await userManager.GetUsersInRoleAsync(RoleNames.Administrator);
        var activeAdministratorIds = administrators
            .Where(user => user.IsActive)
            .Select(user => user.Id)
            .ToHashSet(StringComparer.Ordinal);
        var selectedActiveAdministratorCount = users.Count(user => activeAdministratorIds.Contains(user.Id));
        if (selectedActiveAdministratorCount > 0 &&
            activeAdministratorIds.Count - selectedActiveAdministratorCount < 1)
        {
            ModelState.AddModelError(string.Empty, "系統至少需要保留一個啟用中的系統管理員。");
        }

        if (!ModelState.IsValid)
        {
            await OnGetAsync(cancellationToken);
            return Page();
        }

        foreach (var user in users)
        {
            IdentityResult result;
            try
            {
                result = await userManager.DeleteAsync(user);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                db.ChangeTracker.Clear();
                ModelState.AddModelError(string.Empty, $"使用者「{user.UserName}」已被專案或歷史資料引用，無法刪除；如不再使用，請改為停用帳號。");
                await OnGetAsync(cancellationToken);
                return Page();
            }

            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                await transaction.RollbackAsync(cancellationToken);
                await OnGetAsync(cancellationToken);
                return Page();
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return RedirectToPage("./Index", BuildRouteValues());
    }

    private async Task<string?> GetDeleteBlockReasonAsync(ApplicationUser user)
    {
        if (string.Equals(user.Id, userManager.GetUserId(User), StringComparison.Ordinal))
        {
            return "目前登入中的帳號不能刪除。";
        }

        if (!user.IsActive || !await userManager.IsInRoleAsync(user, RoleNames.Administrator))
        {
            return null;
        }

        var administrators = await userManager.GetUsersInRoleAsync(RoleNames.Administrator);
        return administrators.Any(candidate => candidate.IsActive && candidate.Id != user.Id)
            ? null
            : "系統至少需要保留一個啟用中的系統管理員。";
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
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
            new MetricInsight("使用者總數", TotalCount.ToString("N0"), "系統帳號"),
            new MetricInsight("啟用帳號", activeCount.ToString("N0"), "可參與業務"),
            new MetricInsight("弱管理帳號", weakManagedCount.ToString("N0"), "輕量維護對象", "info")
        ];

        ActiveSlices = ChartPalette.BuildSlices(
        [
            ("啟用", (decimal)activeCount),
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
            items.Add(new FilterSummaryItem("關鍵字", Keyword));
        }

        if (IsActive is not null)
        {
            items.Add(new FilterSummaryItem("啟用", IsActive.Value ? "是" : "否"));
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
