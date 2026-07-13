using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.Settings;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class IndexModel(SystemSettingsService systemSettingsService) : PageModel
{
    [BindProperty]
    public SystemSettingsService.VisualTheme VisualTheme { get; set; } = SystemSettingsService.VisualTheme.Default;

    [BindProperty]
    public UiEffectsLevel UiEffectsLevel { get; set; } = UiEffectsLevel.Medium;

    [BindProperty]
    public SystemSettingsService.DisplayLanguage DisplayLanguage { get; set; } = SystemSettingsService.DisplayLanguage.TraditionalChinese;

    [BindProperty]
    public DateOnly ArchiveDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        VisualTheme = await systemSettingsService.GetVisualThemeAsync(cancellationToken);
        UiEffectsLevel = await systemSettingsService.GetUiEffectsLevelAsync(cancellationToken);
        DisplayLanguage = await systemSettingsService.GetDisplayLanguageAsync(cancellationToken);
        ArchiveDate = await systemSettingsService.GetArchiveDateAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(VisualTheme))
        {
            ModelState.AddModelError(
                nameof(VisualTheme),
                "請選擇有效的顯示主題。");
        }

        if (!Enum.IsDefined(UiEffectsLevel))
        {
            ModelState.AddModelError(
                nameof(UiEffectsLevel),
                "Please choose a valid UI effects level.");
        }

        if (!Enum.IsDefined(DisplayLanguage))
        {
            ModelState.AddModelError(
                nameof(DisplayLanguage),
                "請選擇有效的顯示語言。");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await systemSettingsService.SetVisualThemeAsync(VisualTheme, cancellationToken);
        await systemSettingsService.SetUiEffectsLevelAsync(UiEffectsLevel, cancellationToken);
        await systemSettingsService.SetDisplayLanguageAsync(DisplayLanguage, cancellationToken);
        await systemSettingsService.SetArchiveDateAsync(ArchiveDate, cancellationToken);
        StatusMessage = "設定已儲存。";
        return RedirectToPage();
    }
}
