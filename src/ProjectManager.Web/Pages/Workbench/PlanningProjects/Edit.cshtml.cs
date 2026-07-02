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
    UserManager<ApplicationUser> userManager) : PageModel
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

        Project = project;
        Input = new InputModel
        {
            Id = project.Id,
            Name = project.Name,
            LeaderUserIds = string.IsNullOrWhiteSpace(project.LeaderUserId)
                ? []
                : project.LeaderUserId.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
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
        if (!ModelState.IsValid)
        {
            var project = await planningProjectService.GetPlanningProjectAsync(id, cancellationToken);
            Project = project ?? new PlanningProject();
            await LoadOptionsAsync(cancellationToken);
            return Page();
        }

        var currentUserId = userManager.GetUserId(User);
        // 多选负责人以逗号分隔存储
        var leaderUserId = Input.LeaderUserIds != null && Input.LeaderUserIds.Count > 0
            ? string.Join(",", Input.LeaderUserIds.Where(x => !string.IsNullOrWhiteSpace(x)))
            : null;

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
        public int Id { get; set; }

        [Display(Name = "项目名")]
        [Required(ErrorMessage = "请输入项目名。")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "项目负责人")]
        public List<string>? LeaderUserIds { get; set; }

        [Display(Name = "厂商")]
        public string? Vendor { get; set; }

        [Display(Name = "上期说明")]
        public string? PreviousDescription { get; set; }

        [Display(Name = "年")]
        [Range(2000, 2100, ErrorMessage = "请输入有效年份。")]
        public int? RecordYear { get; set; }

        [Display(Name = "月")]
        [Range(1, 12, ErrorMessage = "请输入有效月份。")]
        public int? RecordMonth { get; set; }

        [Display(Name = "本期记录")]
        public string? CurrentRecord { get; set; }
    }
}
