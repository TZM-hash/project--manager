using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Pages.Shared;
using ProjectManager.Web.Security;

namespace ProjectManager.Web.Pages.Admin.Projects;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class DetailsModel(ApplicationDbContext db) : PageModel
{
    public Project Project { get; private set; } = new();

    public IReadOnlyList<AuditLogDisplayModel> AuditLogs { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var project = await db.Projects
            .AsNoTracking()
            .Include(x => x.Status)
            .ThenInclude(x => x!.Style)
            .Include(x => x.Assignments)
            .ThenInclude(x => x.User)
            .Include(x => x.PurchaseRequests.Where(p => !p.IsDeleted))
            .ThenInclude(x => x.PurchaseStaff)
            .Include(x => x.PurchaseRequests.Where(p => !p.IsDeleted))
            .ThenInclude(x => x.SubCaseContact)
            .Include(x => x.UpdatedByUser)
            .SingleOrDefaultAsync(x => !x.IsDeleted && x.Id == id, cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        Project = project;
        var auditLogs = await db.AuditLogs
            .AsNoTracking()
            .Include(x => x.User)
            .Where(x => x.ProjectId == id || (x.EntityName == "Project" && x.EntityId == id.ToString()))
            .OrderByDescending(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        AuditLogs = AuditLogDisplayModel.FromLogs(auditLogs);

        return Page();
    }
}
