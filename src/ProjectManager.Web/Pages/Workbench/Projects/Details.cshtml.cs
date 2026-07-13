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
    WorkbenchProjectService workbenchProjectService,
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ProjectGanttService ganttService,
    SystemSettingsService systemSettingsService) : PageModel
{
    public Project Project { get; private set; } = new();

    public IReadOnlyList<ProjectStatus> ActiveStatuses { get; private set; } = [];

    public IReadOnlyList<AuditLogDisplayModel> AuditLogs { get; private set; } = [];

    public AuditTrailViewModel AuditTrail { get; private set; } = new([], [], null, null, null, null);

    public bool CanEditProgress { get; private set; }

    public DateOnly ArchiveDate { get; private set; }

    [BindProperty]
    public ProjectGanttInputModel GanttInput { get; set; } = new();

    [TempData]
    public string? GanttMessage { get; set; }

    public IReadOnlyList<string> GanttErrors { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? AuditKeyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AuditAction { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? AuditFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateOnly? AuditTo { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.Leader) || User.IsInRole(RoleNames.Viewer);
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
        var skippedStatuses = await db.ProjectSkippedStatuses
            .AsNoTracking()
            .Where(x => x.ProjectId == id)
            .ToListAsync(cancellationToken);
        foreach (var skippedStatus in skippedStatuses)
        {
            Project.SkippedStatuses.Add(skippedStatus);
        }

        CanEditProgress = User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.ProjectStaff) || User.IsInRole(RoleNames.Leader);
        ArchiveDate = await systemSettingsService.GetArchiveDateAsync(cancellationToken);
        GanttInput = await ganttService.BuildInputAsync(id, cancellationToken);
        ActiveStatuses = await db.ProjectStatuses
            .AsNoTracking()
            .Include(x => x.Style)
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var auditLogs = await db.AuditLogs
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.ProjectId == id || (x.EntityName == "Project" && x.EntityId == id.ToString()))
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        var displayLogs = AuditLogDisplayModel.FromLogs(auditLogs);
        AuditLogs = ApplyAuditFilters(displayLogs);
        AuditTrail = new AuditTrailViewModel(
            AuditLogs,
            BuildActionOptions(displayLogs),
            AuditKeyword,
            AuditAction,
            AuditFrom,
            AuditTo);

        return Page();
    }

    public async Task<IActionResult> OnPostSaveGanttAsync(int id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.Leader) || User.IsInRole(RoleNames.Viewer);
        var project = await workbenchProjectService.GetProjectForUserAsync(
            id,
            userId,
            canViewAll,
            cancellationToken);

        if (project is null)
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
            GanttMessage = "甘特图已儲存。";
            return RedirectToPage("./Details", new { id, Tab = "gantt" });
        }

        var postedInput = GanttInput;
        var result = await OnGetAsync(id, cancellationToken);
        GanttInput = postedInput;
        return result;
    }

    public async Task<IActionResult> OnGetExportGanttAsync(int id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.IsInRole(RoleNames.Administrator) || User.IsInRole(RoleNames.Leader) || User.IsInRole(RoleNames.Viewer);
        var project = await workbenchProjectService.GetProjectForUserAsync(
            id,
            userId,
            canViewAll,
            cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        var file = await ganttService.ExportAsync(id, cancellationToken);
        return File(file.Contents, file.ContentType, file.FileName);
    }

    private IReadOnlyList<AuditLogDisplayModel> ApplyAuditFilters(IReadOnlyList<AuditLogDisplayModel> logs)
    {
        var keyword = AuditKeyword?.Trim();
        var query = logs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(AuditAction))
        {
            query = query.Where(x => x.ActionValue == AuditAction);
        }

        if (AuditFrom is not null)
        {
            query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt.ToLocalTime().Date) >= AuditFrom.Value);
        }

        if (AuditTo is not null)
        {
            query = query.Where(x => DateOnly.FromDateTime(x.CreatedAt.ToLocalTime().Date) <= AuditTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.Actor.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.Action.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.Summary.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                x.Details.Any(detail => AuditLogDisplayModel
                    .FormatDetail(detail)
                    .Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        }

        return query.ToList();
    }

    private static IReadOnlyList<AuditActionOption> BuildActionOptions(IReadOnlyList<AuditLogDisplayModel> logs)
    {
        return logs
            .Where(x => !string.IsNullOrWhiteSpace(x.ActionValue))
            .GroupBy(x => x.ActionValue)
            .Select(x => new AuditActionOption(x.Key, x.First().Action))
            .OrderBy(x => x.Text)
            .ToList();
    }
}
