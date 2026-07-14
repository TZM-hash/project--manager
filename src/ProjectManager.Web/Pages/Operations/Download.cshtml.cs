using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services.Operations;

namespace ProjectManager.Web.Pages.Operations;

[Authorize]
public sealed class DownloadModel(
    OperationJobService jobs,
    OperationFileStore fileStore,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var job = await jobs.GetAsync(id, userId, User.IsInRole(RoleNames.Administrator), cancellationToken);
        if (job is null ||
            job.Status != OperationJobStatus.Succeeded ||
            string.IsNullOrWhiteSpace(job.OutputRelativePath))
        {
            return NotFound();
        }

        try
        {
            return File(
                fileStore.OpenRead(job.OutputRelativePath),
                job.OutputContentType ?? "application/octet-stream",
                job.OutputFileName ?? $"operation-{job.Id}.bin");
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }
}
