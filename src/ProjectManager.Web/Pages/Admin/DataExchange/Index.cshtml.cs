using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.DataExchange;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class IndexModel(
    DataExchangeService dataExchangeService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "請選擇要匯入的 Excel 檔案。")]
    public IFormFile? UploadFile { get; set; }

    public DataImportResult? ImportResult { get; private set; }

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        var file = await dataExchangeService.ExportAllAsync(cancellationToken);
        return File(file.Contents, file.ContentType, file.FileName);
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
            ErrorMessage = "当前仅支持 .xlsx 檔案。";
            return Page();
        }

        try
        {
            await using var stream = UploadFile.OpenReadStream();
            ImportResult = await dataExchangeService.ImportAllAsync(
                stream,
                userManager.GetUserId(User),
                cancellationToken);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"匯入失敗：{ex.Message}";
        }

        return Page();
    }
}
