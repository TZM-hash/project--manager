using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Pages.Shared;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Workbench.Projects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader + "," + RoleNames.Viewer)]
public sealed class DetailsModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ProjectGanttService ganttService,
    SystemSettingsService systemSettingsService,
    AuditTrailQueryService auditTrailQueryService) : PageModel
{
    public Project Project { get; private set; } = new();

    public IReadOnlyList<ProjectStatus> ActiveStatuses { get; private set; } = [];

    public AuditTrailViewModel AuditTrail { get; private set; } = new([], [], null, null, null, null);

    public string ActiveTab { get; private set; } = "overview";

    public bool CanEditProgress { get; private set; }

    public DateOnly ArchiveDate { get; private set; }

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
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.Leader) || User.IsInRole(RoleNames.Viewer);
        ActiveTab = ResolveActiveTab();
        IQueryable<Project> projectQuery = db.Projects
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Id == id)
            .Include(x => x.Status)
            .ThenInclude(x => x!.Style);

        if (!canViewAll)
        {
            projectQuery = projectQuery.Where(x => x.Assignments.Any(a => a.UserId == userId));
        }

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
        CanEditProgress = User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.ProjectStaff) || User.IsInRole(RoleNames.Leader);
        if (ActiveTab == "overview")
        {
            var skippedStatuses = await db.ProjectSkippedStatuses
                .AsNoTracking()
                .Where(x => x.ProjectId == id)
                .ToListAsync(cancellationToken);
            foreach (var skippedStatus in skippedStatuses)
            {
                Project.SkippedStatuses.Add(skippedStatus);
            }

            ActiveStatuses = await db.ProjectStatuses
                .AsNoTracking()
                .Include(x => x.Style)
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }

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
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.Leader) || User.IsInRole(RoleNames.Viewer);
        if (!await CanAccessProjectAsync(id, userId, canViewAll, cancellationToken))
        {
            return NotFound();
        }

        var canEdit = User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.ProjectStaff) || User.IsInRole(RoleNames.Leader);
        if (!canEdit)
        {
            return Forbid();
        }

        GanttErrors = await ganttService.SaveAsync(id, GanttInput, userId, cancellationToken);
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
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.Leader) || User.IsInRole(RoleNames.Viewer);
        if (!await CanAccessProjectAsync(id, userId, canViewAll, cancellationToken))
        {
            return NotFound();
        }

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

    private Task<bool> CanAccessProjectAsync(
        int id,
        string userId,
        bool canViewAll,
        CancellationToken cancellationToken)
    {
        return db.Projects.AnyAsync(
            x => !x.IsDeleted &&
                 x.Id == id &&
                 (canViewAll || x.Assignments.Any(a => a.UserId == userId)),
            cancellationToken);
    }
}
