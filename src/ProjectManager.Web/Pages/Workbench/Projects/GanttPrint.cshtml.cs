using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Workbench.Projects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader + "," + RoleNames.Viewer)]
public sealed class GanttPrintModel(
    WorkbenchProjectService workbenchProjectService,
    UserManager<ApplicationUser> userManager,
    ProjectGanttService ganttService) : PageModel
{
    public Project Project { get; private set; } = new();

    public ProjectGanttInputModel GanttInput { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.IsInRole(RoleNames.Administrator) ||
                         User.IsInRole(RoleNames.Leader) ||
                         User.IsInRole(RoleNames.Viewer);
        var project = await workbenchProjectService.GetProjectForUserAsync(
            id,
            userId,
            canViewAll,
            cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        Project = project;
        GanttInput = await ganttService.BuildInputAsync(id, cancellationToken);
        return Page();
    }
}
