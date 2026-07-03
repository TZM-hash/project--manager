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
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadOptionsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(cancellationToken);
            return Page();
        }

        // 多选负责人以逗号分隔存储
        var leaderUserId = Input.LeaderUserIds != null && Input.LeaderUserIds.Count > 0
            ? string.Join(",", Input.LeaderUserIds.Where(x => !string.IsNullOrWhiteSpace(x)))
            : null;

        var project = new PlanningProject
        {
            Name = Input.Name.Trim(),
            LeaderUserId = leaderUserId,
            Vendor = string.IsNullOrWhiteSpace(Input.Vendor) ? null : Input.Vendor.Trim(),
            LatestDescription = RichTextSanitizer.Normalize(Input.LatestDescription)
        };

        await planningProjectService.CreateAsync(project, cancellationToken);
        return RedirectToPage("./Index");
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        // 只显示项目人员角色
        var projectStaffUsers = await userManager.GetUsersInRoleAsync(RoleNames.ProjectStaff);
        var projectStaffUserIds = projectStaffUsers.Select(x => x.Id).ToHashSet();

        var users = await userManager.Users
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        UserOptions = users
            .Where(x => projectStaffUserIds.Contains(x.Id))
            .Select(x => new SelectListItem(
                string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? x.Id : x.DisplayName,
                x.Id))
            .ToList();
    }

    public sealed class InputModel
    {
        [Display(Name = "项目名")]
        [Required(ErrorMessage = "请输入项目名。")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "项目负责人")]
        public List<string>? LeaderUserIds { get; set; }

        [Display(Name = "厂商")]
        public string? Vendor { get; set; }

        [Display(Name = "最新说明")]
        public string? LatestDescription { get; set; }
    }
}
