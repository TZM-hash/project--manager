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

[Authorize(Roles = RoleNames.BusinessDataRoles)]
public sealed class DetailsModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ProjectGanttService ganttService,
    SystemSettingsService systemSettingsService,
    AuditTrailQueryService auditTrailQueryService,
    ProjectCollaborationService collaborationService,
    ProjectCollaborationAttachmentStore attachmentStore) : PageModel
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

    public CollaborationPage Collaboration { get; private set; } = new(false, [], 0, 1, 20, 1);

    [BindProperty]
    public ProjectCollaborationInputModel CollaborationInput { get; set; } = new();

    [TempData]
    public string? CollaborationMessage { get; set; }

    public IReadOnlyList<string> CollaborationErrors { get; private set; } = [];

    [BindProperty(SupportsGet = true)]
    public int CollaborationPage { get; set; } = 1;

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
        var canViewAll = User.CanViewAllBusinessData();
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
        CanEditProgress = User.CanManageAllBusinessData() || User.IsInRole(RoleNames.ProjectStaff);
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

        if (ActiveTab == "collaboration")
        {
            Collaboration = await collaborationService.GetPageAsync(
                id, userId, canViewAll, CollaborationPage, 20, cancellationToken);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveGanttAsync(int id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.CanViewAllBusinessData();
        if (!await CanAccessProjectAsync(id, userId, canViewAll, cancellationToken))
        {
            return NotFound();
        }

        var canEdit = User.CanManageAllBusinessData() || User.IsInRole(RoleNames.ProjectStaff);
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
        var canViewAll = User.CanViewAllBusinessData();
        if (!await CanAccessProjectAsync(id, userId, canViewAll, cancellationToken))
        {
            return NotFound();
        }

        var file = await ganttService.ExportAsync(id, cancellationToken);
        return File(file.Contents, file.ContentType, file.FileName);
    }

    public Task<IActionResult> OnPostAddCollaborationAsync(int id, CancellationToken cancellationToken) =>
        SaveCollaborationAsync(id, isUpdate: false, cancellationToken);

    public Task<IActionResult> OnPostUpdateCollaborationAsync(int id, CancellationToken cancellationToken) =>
        SaveCollaborationAsync(id, isUpdate: true, cancellationToken);

    public async Task<IActionResult> OnPostDeleteCollaborationAsync(
        int id,
        int recordId,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.CanViewAllBusinessData();
        var canEditAll = User.CanManageAllBusinessData();
        if (!canEditAll && !User.IsInRole(RoleNames.ProjectStaff))
        {
            return Forbid();
        }
        var result = await collaborationService.DeleteAsync(
            new CollaborationCommand(id, recordId, userId, canViewAll, canEditAll, null, string.Empty, rowVersion),
            cancellationToken);
        return await CompleteCollaborationCommandAsync(id, result, "協作記錄已刪除。", cancellationToken);
    }

    private async Task<IActionResult> SaveCollaborationAsync(int id, bool isUpdate, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.CanViewAllBusinessData();
        var canEditAll = User.CanManageAllBusinessData();
        if (!canEditAll && !User.IsInRole(RoleNames.ProjectStaff))
        {
            return Forbid();
        }
        var command = new CollaborationCommand(
            id,
            CollaborationInput.RecordId,
            userId,
            canViewAll,
            canEditAll,
            CollaborationInput.Category,
            CollaborationInput.Content,
            CollaborationInput.RowVersion,
            CollaborationInput.IsImportant);
        var result = isUpdate
            ? await collaborationService.UpdateAsync(command, cancellationToken)
            : await collaborationService.AddAsync(command, cancellationToken);
        if (result.Success && !isUpdate && CollaborationInput.Attachment is not null && result.Record is not null)
        {
            try
            {
                var upload = CollaborationInput.Attachment;
                await using var uploadStream = upload.OpenReadStream();
                var stored = await attachmentStore.SaveAsync(upload.FileName, upload.ContentType, upload.Length, uploadStream, cancellationToken);
                await collaborationService.AddAttachmentAsync(id, result.Record.Id, userId, canViewAll, canEditAll,
                    new ProjectCollaborationAttachment
                    {
                        OriginalFileName = stored.OriginalFileName,
                        RelativePath = stored.RelativePath,
                        ContentType = stored.ContentType,
                        Length = stored.Length
                    }, cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                CollaborationErrors = [exception.Message];
                result = new CollaborationResult(false, CollaborationErrors, false, result.Record);
            }
        }
        return await CompleteCollaborationCommandAsync(
            id,
            result,
            isUpdate ? "協作記錄已更新。" : "協作記錄已新增。",
            cancellationToken);
    }

    public async Task<IActionResult> OnGetDownloadCollaborationAttachmentAsync(int id, int attachmentId, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var canViewAll = User.CanViewAllBusinessData();
        if (!await CanAccessProjectAsync(id, userId, canViewAll, cancellationToken)) return NotFound();
        var attachment = await db.ProjectCollaborationAttachments.AsNoTracking()
            .Include(x => x.Record)
            .SingleOrDefaultAsync(x => x.Id == attachmentId && x.Record!.ProjectId == id, cancellationToken);
        if (attachment is null) return NotFound();
        try { return File(attachmentStore.OpenRead(attachment.RelativePath), attachment.ContentType, attachment.OriginalFileName); }
        catch (FileNotFoundException) { return NotFound(); }
    }

    private async Task<IActionResult> CompleteCollaborationCommandAsync(
        int id,
        CollaborationResult result,
        string successMessage,
        CancellationToken cancellationToken)
    {
        if (result.Success)
        {
            CollaborationMessage = successMessage;
            return RedirectToPage("./Details", new { id, Tab = "collaboration" });
        }

        CollaborationErrors = result.Errors;
        var postedInput = CollaborationInput;
        Tab = "collaboration";
        var pageResult = await OnGetAsync(id, cancellationToken);
        CollaborationInput = postedInput;
        return pageResult;
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

        return Tab?.Trim().ToLowerInvariant() is "profile" or "purchases" or "gantt" or "collaboration" or "audit"
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
