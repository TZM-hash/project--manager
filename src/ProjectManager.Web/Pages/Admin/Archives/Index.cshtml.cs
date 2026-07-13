using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Extensions;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.Archives;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class IndexModel(
    ApplicationDbContext db,
    ProjectArchiveService projectArchiveService) : PageModel
{
    public IReadOnlyList<ProjectArchive> Archives { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Archives = await db.ProjectArchives
            .AsNoTracking()
            .Include(x => x.ArchivedByUser)
            .OrderByDescending(x => x.ArchivedAt)
            .ToListAsync(cancellationToken);
    }

    public bool CanRestore(ProjectArchive archive)
    {
        return projectArchiveService.CanRestoreProjectArchive(archive);
    }

    public async Task<IActionResult> OnPostRestoreAsync(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var result = await projectArchiveService.RestoreProjectAsync(id, userId, cancellationToken);

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Error ?? "還原失敗";
        }
        else
        {
            TempData["SuccessMessage"] = "專案已成功還原到專案總覽";
        }

        return RedirectToPage();
    }
}