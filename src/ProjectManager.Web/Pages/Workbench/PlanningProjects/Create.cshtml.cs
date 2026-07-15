using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Workbench.PlanningProjects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader)]
public sealed class CreateModel(
    PlanningProjectService planningProjectService,
    UserManager<ApplicationUser> userManager,
    UserLookupService userLookup) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadOptionsAsync(cancellationToken);
        if (!User.CanManageAllBusinessData())
        {
            Input.LeaderUserId = userManager.GetUserId(User);
        }
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var currentUserId = userManager.GetUserId(User);
        var leaderUserId = User.CanManageAllBusinessData() ? Input.LeaderUserId : currentUserId;
        if (leaderUserId?.Contains(',') == true)
        {
            ModelState.AddModelError(nameof(Input.LeaderUserId), "規劃中專案目前只能指定一位負責人。");
        }
        else if (!string.IsNullOrWhiteSpace(leaderUserId))
        {
            var resolvedLeaderUserId = await userLookup.ResolveActiveProjectStaffUserIdAsync(
                leaderUserId,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(resolvedLeaderUserId))
            {
                ModelState.AddModelError(nameof(Input.LeaderUserId), "請選擇有效的一般使用者作為負責人。");
            }
            else
            {
                leaderUserId = resolvedLeaderUserId;
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(cancellationToken);
            return Page();
        }

        var project = new PlanningProject
        {
            Name = Input.Name.Trim(),
            LeaderUserId = leaderUserId,
            Vendor = string.IsNullOrWhiteSpace(Input.Vendor) ? null : Input.Vendor.Trim(),
            LatestDescription = RichTextSanitizer.Normalize(Input.LatestDescription)
        };

        await planningProjectService.CreateAsync(project, cancellationToken);
        TempData["SuccessMessage"] = $"規劃中專案「{project.Name}」已新增。";
        return RedirectToPage("./Index");
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        // 只顯示專案人員角色
        var projectStaffUsers = await userManager.GetUsersInRoleAsync(RoleNames.ProjectStaff);
        var projectStaffUserIds = projectStaffUsers.Select(x => x.Id).ToHashSet();

        var users = await userManager.Users
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        var currentUserId = userManager.GetUserId(User);
        UserOptions = users
            .Where(x => User.CanManageAllBusinessData()
                ? projectStaffUserIds.Contains(x.Id)
                : x.Id == currentUserId)
            .Select(x => new SelectListItem(
                string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? x.Id : x.DisplayName,
                x.Id))
            .ToList();
    }

    public sealed class InputModel
    {
        [Display(Name = "專案名")]
        [Required(ErrorMessage = "请输入專案名。")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "暫定負責人")]
        public string? LeaderUserId { get; set; }

        [Display(Name = "暫定廠商")]
        public string? Vendor { get; set; }

        [Display(Name = "最新說明")]
        public string? LatestDescription { get; set; }
    }
}
