using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Reports.OpenProjects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.Leader + "," + RoleNames.Viewer + "," + RoleNames.ProjectStaff)]
public sealed class IndexModel(
    ProjectQueryService projectQueryService,
    ExcelReportService excelReportService,
    ApplicationDbContext db,
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

    public IReadOnlyList<Project> Projects { get; private set; } = [];

    public List<SelectListItem> StatusOptions { get; private set; } = [];

    public List<SelectListItem> UserOptions { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadOptionsAsync(cancellationToken);
        Projects = await projectQueryService.GetProjectsAsync(CreateFilter(), cancellationToken);
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        var file = await excelReportService.ExportOpenProjectsAsync(CreateFilter(), cancellationToken);
        return File(file.Contents, file.ContentType, file.FileName);
    }

    private ProjectFilter CreateFilter()
    {
        var personnelUserId = User.IsInRole(RoleNames.ProjectStaff)
            ? userManager.GetUserId(User)
            : PersonnelUserId;

        return new ProjectFilter(
            Year,
            ParentCaseNumber,
            ProjectNumber,
            ProjectName,
            personnelUserId,
            StatusId,
            OpenOnly: true);
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        StatusOptions = await db.ProjectStatuses
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString()))
            .ToListAsync(cancellationToken);

        var users = await userManager.Users
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        UserOptions = users
            .Select(x => new SelectListItem(
                string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? x.Id : x.DisplayName,
                x.Id))
            .ToList();
    }
}
