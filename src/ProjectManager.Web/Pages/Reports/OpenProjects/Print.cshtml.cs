using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Reports.OpenProjects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.Leader + "," + RoleNames.Viewer + "," + RoleNames.ProjectStaff)]
public sealed class PrintModel(
    ProjectQueryService projectQueryService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Year { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ParentCaseNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ProjectNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ProjectName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? PersonnelUserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? StatusId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AnalysisType { get; set; }

    public IReadOnlyList<Project> Projects { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var personnelUserId = User.CanManageAllBusinessData()
            ? PersonnelUserId
            : userManager.GetUserId(User);

        Projects = await projectQueryService.GetProjectsAsync(
            new ProjectFilter(Year, ParentCaseNumber, ProjectNumber, ProjectName, personnelUserId, StatusId, OpenOnly: true, AnalysisType),
            cancellationToken);
    }
}
