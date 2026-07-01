using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.Projects;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class EditModel(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ProjectMaintenanceService maintenanceService,
    AuditLogService auditLogService)
    : ProjectFormPageModel(db, userManager, maintenanceService)
{
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
        ApplyProjectValues(project, validation.Project, now);
        SyncAssignments(project);
        SyncPurchaseRequests(project, now);

        await Db.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(
            UserManager.GetUserId(User),
            "Update",
            "Project",
            project.Id.ToString(),
            $"Updated project {project.ProjectNumber}.",
            cancellationToken);
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
        project.IsDeleted = true;
        project.UpdatedAt = now;
        project.UpdatedByUserId = UserManager.GetUserId(User);

        foreach (var request in project.PurchaseRequests)
        {
            request.IsDeleted = true;
            request.UpdatedAt = now;
        }

        await Db.SaveChangesAsync(cancellationToken);
        await auditLogService.LogAsync(
            UserManager.GetUserId(User),
            "Delete",
            "Project",
            project.Id.ToString(),
            $"Deleted project {project.ProjectNumber}.",
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
