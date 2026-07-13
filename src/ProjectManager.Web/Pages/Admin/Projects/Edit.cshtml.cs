using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.Projects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader)]
public sealed class EditModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ProjectMaintenanceService maintenanceService,
    AuditLogService auditLogService)
    : ProjectFormPageModel(db, userManager, maintenanceService)
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnTab { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var project = await FindProjectAsync(id, asTracking: false, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        Input = CreateInput(project);
        EnsureBlankPurchaseRows(2);
        await LoadOptionsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        var project = await FindProjectAsync(id, asTracking: true, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        var validation = await ValidateFormAsync(id, cancellationToken);
        if (!validation.IsValid)
        {
            Input.Id = id;
            EnsureBlankPurchaseRows(2);
            await LoadOptionsAsync(cancellationToken);
            return Page();
        }

        var now = DateTimeOffset.UtcNow;
        // 修改前先取快照，后面 EF 跟踪实体会被表单值覆盖，不能再从实体读旧值。
        var before = ProjectAuditChangeBuilder.CreateSnapshot(project);
        ApplyProjectValues(project, validation.Project, now);
        SyncAssignments(project);
        SyncPurchaseRequests(project, now);

        await Db.SaveChangesAsync(cancellationToken);
        // 儲存后再生成新快照，欄位级和请购级差異都由统一 builder 計算。
        var after = ProjectAuditChangeBuilder.CreateSnapshot(project);
        var changes = ProjectAuditChangeBuilder.BuildUpdateChanges(before, after);
        if (changes.Count > 0)
        {
            await auditLogService.LogProjectChangeAsync(
                UserManager.GetUserId(User),
                "Update",
                project.Id,
                project.ProjectNumber,
                $"修改專案 {project.ProjectNumber}",
                changes,
                cancellationToken);
        }

        return RedirectToPage("./Details", new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        var project = await FindProjectAsync(id, asTracking: true, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        var now = DateTimeOffset.UtcNow;
        // 刪除日誌需要保留刪除前的工号、名稱、金額等上下文。
        var before = ProjectAuditChangeBuilder.CreateSnapshot(project);
        project.IsDeleted = true;
        project.UpdatedAt = now;
        project.UpdatedByUserId = UserManager.GetUserId(User);

        foreach (var request in project.PurchaseRequests)
        {
            request.IsDeleted = true;
            request.UpdatedAt = now;
        }

        await Db.SaveChangesAsync(cancellationToken);
        await auditLogService.LogProjectChangeAsync(
            UserManager.GetUserId(User),
            "Delete",
            project.Id,
            project.ProjectNumber,
            $"刪除專案 {project.ProjectNumber}",
            ProjectAuditChangeBuilder.BuildDeleteChanges(before),
            cancellationToken);
        return RedirectToPage("./Index");
    }

    private async Task<Project?> FindProjectAsync(
        int id,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        var query = Db.Projects
            .Include(x => x.Assignments)
            .Include(x => x.PurchaseRequests)
            .Where(x => !x.IsDeleted && x.Id == id);

        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }
}
