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
public sealed class EditModel(
    PlanningProjectService planningProjectService,
    UserManager<ApplicationUser> userManager,
    UserLookupService userLookup) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public PlanningProject Project { get; private set; } = new();

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var project = await planningProjectService.GetPlanningProjectAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        if (!CanManage(project))
        {
            return NotFound();
        }

        Project = project;
        Input = new InputModel
        {
            Id = project.Id,
            Name = project.Name,
            LeaderUserId = project.LeaderUserId,
            Vendor = project.Vendor,
            PreviousDescription = project.LatestDescription,
            RecordYear = DateTime.Today.Year,
            RecordMonth = DateTime.Today.Month
        };

        await LoadOptionsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        var project = await planningProjectService.GetPlanningProjectAsync(id, cancellationToken);
        if (project is null || !CanManage(project))
        {
            return NotFound();
        }

        var currentUserId = userManager.GetUserId(User);
        var leaderUserId = User.CanManageAllBusinessData() ? Input.LeaderUserId : project.LeaderUserId;
        if (leaderUserId?.Contains(',') == true)
        {
            ModelState.AddModelError(nameof(Input.LeaderUserId), "規劃中專案目前只能指定一位負責人。");
        }
        else if (User.CanManageAllBusinessData() && !string.IsNullOrWhiteSpace(leaderUserId))
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
            Project = project;
            await LoadOptionsAsync(cancellationToken);
            return Page();
        }

        var updated = await planningProjectService.UpdateAsync(
            id,
            Input.Name.Trim(),
            leaderUserId,
            Input.Vendor,
            null,
            Input.RecordYear,
            Input.RecordMonth,
            Input.CurrentRecord,
            currentUserId,
            cancellationToken);

        if (updated is null)
        {
            return NotFound();
        }

        TempData["SuccessMessage"] = $"規劃中專案「{updated.Name}」已更新。";
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

    private bool CanManage(PlanningProject project)
    {
        if (User.CanManageAllBusinessData())
        {
            return true;
        }

        var currentUserId = userManager.GetUserId(User);
        return !string.IsNullOrWhiteSpace(currentUserId) &&
               (project.LeaderUserId ?? string.Empty)
                   .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Contains(currentUserId, StringComparer.Ordinal);
    }

    public sealed class InputModel
    {
        public int Id { get; set; }

        [Display(Name = "專案名")]
        [Required(ErrorMessage = "请输入專案名。")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "專案負責人")]
        public string? LeaderUserId { get; set; }

        [Display(Name = "廠商")]
        public string? Vendor { get; set; }

        [Display(Name = "上期說明")]
        public string? PreviousDescription { get; set; }

        [Display(Name = "年")]
        [Range(2000, 2100, ErrorMessage = "请输入有效年份。")]
        public int? RecordYear { get; set; }

        [Display(Name = "月")]
        [Range(1, 12, ErrorMessage = "请输入有效月份。")]
        public int? RecordMonth { get; set; }

        [Display(Name = "本期記錄")]
        public string? CurrentRecord { get; set; }
    }
}
