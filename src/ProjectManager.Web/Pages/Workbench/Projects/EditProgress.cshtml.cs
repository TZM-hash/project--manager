using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Workbench.Projects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader)]
public sealed class EditProgressModel(
    WorkbenchProjectService workbenchProjectService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool HasConcurrencyConflict { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var canEditAll = User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.Leader);
        var project = await workbenchProjectService.GetProjectForUserAsync(
            id,
            userId,
            canEditAll,
            cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            ProjectId = project.Id,
            ProjectNumber = project.ProjectNumber,
            ProjectName = project.Name,
            ProgressPercent = project.ProgressPercent,
            ProgressDescription = project.ProgressDescription,
            RowVersion = Convert.ToBase64String(project.RowVersion)
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        Input.ProjectId = id;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var userId = userManager.GetUserId(User) ?? string.Empty;
        var result = await workbenchProjectService.UpdateProgressAsync(
            new UpdateProgressRequest(
                id,
                userId,
                CanEditAll: User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.Leader),
                Input.ProgressPercent,
                Input.ProgressDescription,
                Input.RowVersion),
            cancellationToken);

        if (!result.Success)
        {
            HasConcurrencyConflict = result.IsConcurrencyConflict;
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return Page();
        }

        return RedirectToPage("./Details", new { id });
    }

    public sealed class InputModel
    {
        public int ProjectId { get; set; }

        public string ProjectNumber { get; set; } = string.Empty;

        public string ProjectName { get; set; } = string.Empty;

        public decimal ProgressPercent { get; set; }

        public string? ProgressDescription { get; set; }

        public string RowVersion { get; set; } = string.Empty;
    }
}
