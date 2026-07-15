using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services.Operations;

namespace ProjectManager.Web.Pages.Admin.DataExchange;

[Authorize(Roles = RoleNames.BusinessManagerRoles)]
public sealed class IndexModel(
    OperationJobService jobs,
    OperationFileStore fileStore,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "請選擇要匯入的 Excel 檔案。")]
    public IFormFile? UploadFile { get; set; }

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostExportAsync(CancellationToken cancellationToken)
    {
        var job = await jobs.QueueAsync(
            OperationJobType.FullExport,
            RequiredUserId(),
            null,
            null,
            cancellationToken);
        return RedirectToPage("/Operations/Index", new { jobId = job.Id });
    }

    public async Task<IActionResult> OnPostImportAsync(CancellationToken cancellationToken)
    {
        if (UploadFile is null || UploadFile.Length == 0)
        {
            ErrorMessage = "請選擇有效的 Excel 檔案。";
            return Page();
        }

        var extension = Path.GetExtension(UploadFile.FileName);
        if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "目前僅支援 .xlsx 檔案。";
            return Page();
        }

        await using var stream = UploadFile.OpenReadStream();
        var stored = await fileStore.SaveAsync("input", UploadFile.FileName, stream, cancellationToken);
        var job = await jobs.QueueAsync(
            OperationJobType.FullImport,
            RequiredUserId(),
            null,
            stored.RelativePath,
            cancellationToken);
        return RedirectToPage("/Operations/Index", new { jobId = job.Id });
    }

    private string RequiredUserId() =>
        userManager.GetUserId(User) ?? throw new InvalidOperationException("找不到目前使用者。");
}
