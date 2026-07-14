using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Pages.Shared;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.Projects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader)]
public sealed class DetailsModel(
    ApplicationDbContext db,
    ProjectGanttService ganttService,
    SystemSettingsService systemSettingsService,
    AuditTrailQueryService auditTrailQueryService) : PageModel
{
    public Project Project { get; private set; } = new();

    public DateOnly ArchiveDate { get; private set; }

    public AuditTrailViewModel AuditTrail { get; private set; } = new([], [], null, null, null, null);

    public string ActiveTab { get; private set; } = "overview";

    [BindProperty]
    public ProjectGanttInputModel GanttInput { get; set; } = new();

    [TempData]
    public string? GanttMessage { get; set; }

    public IReadOnlyList<string> GanttErrors { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Tab { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AuditKeyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AuditAction { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? AuditFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? AuditTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public int AuditPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int AuditPageSize { get; set; } = AuditTrailQueryService.DefaultPageSize;

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        ActiveTab = ResolveActiveTab();
        IQueryable<Project> projectQuery = db.Projects
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Id == id)
            .Include(x => x.Status)
            .ThenInclude(x => x!.Style);

        projectQuery = ActiveTab switch
        {
            "overview" => projectQuery
                .Include(x => x.PurchaseRequests.Where(p => !p.IsDeleted)),
            "profile" => projectQuery
                .Include(x => x.Assignments)
                .ThenInclude(x => x.User)
                .Include(x => x.UpdatedByUser),
            "purchases" => projectQuery
                .Include(x => x.PurchaseRequests.Where(p => !p.IsDeleted))
                .ThenInclude(x => x.PurchaseStaff)
                .Include(x => x.PurchaseRequests.Where(p => !p.IsDeleted))
                .ThenInclude(x => x.SubCaseContact),
            "gantt" => projectQuery
                .Include(x => x.Assignments)
                .ThenInclude(x => x.User),
            _ => projectQuery
        };

        var project = await projectQuery
            .AsSplitQuery()
            .SingleOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        Project = project;
        if (ActiveTab == "gantt")
        {
            ArchiveDate = await systemSettingsService.GetArchiveDateAsync(cancellationToken);
            GanttInput = await ganttService.BuildInputAsync(id, cancellationToken);
        }

        if (ActiveTab == "audit")
        {
            AuditTrail = await auditTrailQueryService.BuildAsync(
                id,
                AuditKeyword,
                AuditAction,
                AuditFrom,
                AuditTo,
                AuditPage,
                AuditPageSize,
                cancellationToken);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveGanttAsync(int id, CancellationToken cancellationToken)
    {
        GanttErrors = await ganttService.SaveAsync(
            id,
            GanttInput,
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            cancellationToken);

        if (GanttErrors.Count == 0)
        {
            GanttMessage = "甘特圖已儲存。";
            return RedirectToPage("./Details", new { id, Tab = "gantt" });
        }

        var postedInput = GanttInput;
        Tab = "gantt";
        var result = await OnGetAsync(id, cancellationToken);
        GanttInput = postedInput;
        return result;
    }

    public async Task<IActionResult> OnGetExportGanttAsync(int id, CancellationToken cancellationToken)
    {
        var file = await ganttService.ExportAsync(id, cancellationToken);
        return File(file.Contents, file.ContentType, file.FileName);
    }

    private string ResolveActiveTab()
    {
        if (!string.IsNullOrWhiteSpace(AuditKeyword) ||
            !string.IsNullOrWhiteSpace(AuditAction) ||
            AuditFrom is not null ||
            AuditTo is not null ||
            AuditPage > 1)
        {
            return "audit";
        }

        return Tab?.Trim().ToLowerInvariant() is "profile" or "purchases" or "gantt" or "audit"
            ? Tab.Trim().ToLowerInvariant()
            : "overview";
    }
}
