using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Workbench.Projects;

[Authorize(Roles = RoleNames.ProjectStaff + "," + RoleNames.Leader + "," + RoleNames.Viewer)]
public sealed class DetailsModel(
    WorkbenchProjectService workbenchProjectService,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public Project Project { get; private set; } = new();

    public IReadOnlyList<ProjectStatus> ActiveStatuses { get; private set; } = [];

    public bool CanEditProgress { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.IsInRole(RoleNames.Leader) || User.IsInRole(RoleNames.Viewer);
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
        CanEditProgress = User.IsInRole(RoleNames.ProjectStaff) || User.IsInRole(RoleNames.Leader);
        ActiveStatuses = await db.ProjectStatuses
            .AsNoTracking()
            .Include(x => x.Style)
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Page();
    }
}
