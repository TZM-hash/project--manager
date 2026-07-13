using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.Projects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader)]
public sealed class CreateModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ProjectMaintenanceService maintenanceService,
    AuditLogService auditLogService)
    : ProjectFormPageModel(db, userManager, maintenanceService)
{
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        EnsureBlankPurchaseRows(3);
        await LoadOptionsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var validation = await ValidateFormAsync(null, cancellationToken);
        if (!validation.IsValid)
        {
            EnsureBlankPurchaseRows(2);
            await LoadOptionsAsync(cancellationToken);
            return Page();
        }

        var now = DateTimeOffset.UtcNow;
        var project = validation.Project;
        project.CreatedAt = now;
        ApplyProjectValues(project, validation.Project, now);
        SyncAssignments(project);

        foreach (var request in validation.PurchaseRequests)
        {
            request.CreatedAt = now;
            request.UpdatedAt = now;
            project.PurchaseRequests.Add(request);
        }

        Db.Projects.Add(project);
        await Db.SaveChangesAsync(cancellationToken);

        // 新增專案的審計明細以“空 -> 初始值”方式記錄，方便后续回溯專案最初录入內容。
        var changes = ProjectAuditChangeBuilder.BuildCreateChanges(
            ProjectAuditChangeBuilder.CreateSnapshot(project));
        await auditLogService.LogProjectChangeAsync(
            UserManager.GetUserId(User),
            "Create",
            project.Id,
            project.ProjectNumber,
            $"新增專案 {project.ProjectNumber}",
            changes,
            cancellationToken);
        return RedirectToPage("./Details", new { id = project.Id });
    }
}
