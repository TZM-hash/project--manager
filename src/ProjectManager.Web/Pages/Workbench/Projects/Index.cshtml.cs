using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Workbench.Projects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader + "," + RoleNames.Viewer)]
public sealed class IndexModel(
    WorkbenchProjectService workbenchProjectService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public IReadOnlyList<Project> Projects { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.Leader) || User.IsInRole(RoleNames.Viewer);

        Projects = await workbenchProjectService.GetProjectsForUserAsync(
            userId,
            canViewAll,
            cancellationToken);
    }
}
