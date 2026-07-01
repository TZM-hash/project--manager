using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Settlements;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.Leader)]
public sealed class CreateModel(
    SettlementService settlementService,
    ProjectQueryService projectQueryService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IReadOnlyList<Project> PreviewProjects { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Input.Year = DateTime.Today.Year;
        Input.Month = DateTime.Today.Month;
        await LoadPreviewAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadPreviewAsync(cancellationToken);
            return Page();
        }

        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            ModelState.AddModelError(string.Empty, "无法识别当前用户。");
            await LoadPreviewAsync(cancellationToken);
            return Page();
        }

        var result = await settlementService.CreateAsync(
            new CreateSettlementRequest(Input.Year, Input.Month, userId, Input.Notes),
            cancellationToken);

        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            await LoadPreviewAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage("./Details", new { id = result.BatchId });
    }

    private async Task LoadPreviewAsync(CancellationToken cancellationToken)
    {
        PreviewProjects = await projectQueryService.GetProjectsAsync(
            new ProjectFilter(null, null, null, null, null, null, OpenOnly: false),
            cancellationToken);
    }

    public sealed class InputModel
    {
        public int Year { get; set; }

        public int Month { get; set; }

        public string? Notes { get; set; }
    }
}
