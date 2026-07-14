using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services.Operations;

namespace ProjectManager.Web.Pages.Operations;

[Authorize]
public sealed class StatusModel(
    OperationJobService jobs,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? string.Empty;
        var job = await jobs.GetAsync(id, userId, User.IsInRole(RoleNames.Administrator), cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return new JsonResult(new
        {
            job.Id,
            Status = job.Status.ToString(),
            StatusText = IndexModel.StatusText(job.Status),
            job.ProgressPercent,
            job.StatusMessage,
            job.ResultSummary,
            ErrorDetails = job.ErrorDetails is { Length: > 2000 } ? job.ErrorDetails[..2000] : job.ErrorDetails,
            IsTerminal = job.Status is OperationJobStatus.Succeeded or OperationJobStatus.Failed or OperationJobStatus.Cancelled,
            DownloadUrl = job.Status == OperationJobStatus.Succeeded && !string.IsNullOrWhiteSpace(job.OutputRelativePath)
                ? Url.Page("./Download", new { id = job.Id })
                : null
        });
    }
}
