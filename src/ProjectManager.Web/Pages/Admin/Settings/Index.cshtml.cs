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
    public UiEffectsLevel UiEffectsLevel { get; set; } = UiEffectsLevel.Medium;

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        UiEffectsLevel = await systemSettingsService.GetUiEffectsLevelAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(UiEffectsLevel))
        {
            ModelState.AddModelError(
                nameof(UiEffectsLevel),
                "Please choose a valid UI effects level.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await systemSettingsService.SetUiEffectsLevelAsync(UiEffectsLevel, cancellationToken);
        StatusMessage = "UI effects level saved.";
        return RedirectToPage();
    }
}
