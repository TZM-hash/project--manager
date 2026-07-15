using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Workbench.PlanningProjects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader + "," + RoleNames.Viewer)]
public sealed class PrintModel(
    PlanningProjectService planningProjectService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public PlanningProject Project { get; private set; } = new();

    public string LeaderDisplayNames { get; private set; } = "-";

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var project = await planningProjectService.GetPlanningProjectAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        if (!User.CanManageAllBusinessData())
        {
            var currentUserId = userManager.GetUserId(User);
            var leaderIds = (project.LeaderUserId ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (string.IsNullOrWhiteSpace(currentUserId) ||
                !leaderIds.Contains(currentUserId, StringComparer.Ordinal))
            {
                return NotFound();
            }
        }

        Project = project;

        // 解析逗号分隔的負責人 ID，顯示姓名
        if (!string.IsNullOrWhiteSpace(project.LeaderUserId))
        {
            var ids = project.LeaderUserId.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var users = await userManager.Users
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            var userDict = users.ToDictionary(
                x => x.Id,
                x => string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? x.Id : x.DisplayName);

            var names = ids
                .Where(id => userDict.TryGetValue(id, out _))
                .Select(id => userDict[id])
                .ToList();
            LeaderDisplayNames = names.Count > 0 ? string.Join("、", names) : "-";
        }

        return Page();
    }
}
