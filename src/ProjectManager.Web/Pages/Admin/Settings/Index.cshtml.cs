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
    public SystemSettingsService.MotionStyle MotionStyle { get; set; } = SystemSettingsService.MotionStyle.Default;

    [BindProperty]
    public UiEffectsLevel UiEffectsLevel { get; set; } = UiEffectsLevel.Medium;

    [BindProperty]
    public SystemSettingsService.DisplayLanguage DisplayLanguage { get; set; } = SystemSettingsService.DisplayLanguage.TraditionalChinese;

    [BindProperty]
    public SystemSettingsService.GlobalFont GlobalFont { get; set; } = SystemSettingsService.GlobalFont.SystemDefault;

    [BindProperty]
    public DateOnly ArchiveDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        VisualTheme = await systemSettingsService.GetVisualThemeAsync(cancellationToken);
        MotionStyle = await systemSettingsService.GetMotionStyleAsync(cancellationToken);
        UiEffectsLevel = await systemSettingsService.GetUiEffectsLevelAsync(cancellationToken);
        DisplayLanguage = await systemSettingsService.GetDisplayLanguageAsync(cancellationToken);
        GlobalFont = await systemSettingsService.GetGlobalFontAsync(cancellationToken);
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

        if (!Enum.IsDefined(MotionStyle))
        {
            ModelState.AddModelError(
                nameof(MotionStyle),
                "請選擇有效的動效風格。");
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

        if (!Enum.IsDefined(GlobalFont))
        {
            ModelState.AddModelError(
                nameof(GlobalFont),
                "請選擇有效的全局字體。");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await systemSettingsService.SetVisualThemeAsync(VisualTheme, cancellationToken);
        await systemSettingsService.SetMotionStyleAsync(MotionStyle, cancellationToken);
        await systemSettingsService.SetUiEffectsLevelAsync(UiEffectsLevel, cancellationToken);
        await systemSettingsService.SetDisplayLanguageAsync(DisplayLanguage, cancellationToken);
        await systemSettingsService.SetGlobalFontAsync(GlobalFont, cancellationToken);
        await systemSettingsService.SetArchiveDateAsync(ArchiveDate, cancellationToken);
        StatusMessage = "設定已儲存。";
        return RedirectToPage();
    }
}
