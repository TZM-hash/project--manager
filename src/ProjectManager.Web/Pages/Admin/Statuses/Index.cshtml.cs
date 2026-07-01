using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.Statuses;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class IndexModel(
    ApplicationDbContext db,
    StatusMaintenanceService statusMaintenanceService) : PageModel
{
    public IReadOnlyList<StatusListItem> Statuses { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadStatusesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        var result = await statusMaintenanceService.DeleteAsync(id, cancellationToken);
        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            await LoadStatusesAsync(cancellationToken);
            return Page();
        }

        return RedirectToPage("./Index");
    }

    private async Task LoadStatusesAsync(CancellationToken cancellationToken)
    {
        Statuses = await db.ProjectStatuses
            .AsNoTracking()
            .Include(x => x.Style)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new StatusListItem(
                x.Id,
                x.Code,
                x.Name,
                x.SortOrder,
                x.IsClosed,
                x.IsActive,
                x.Style == null ? "#1f2937" : x.Style.TextColor,
                x.Style == null ? "#e5e7eb" : x.Style.BackgroundColor,
                x.Style != null && x.Style.IsBold,
                db.Projects.Count(p => !p.IsDeleted && p.StatusId == x.Id)))
            .ToListAsync(cancellationToken);
    }

    public sealed record StatusListItem(
        int Id,
        string Code,
        string Name,
        int SortOrder,
        bool IsClosed,
        bool IsActive,
        string TextColor,
        string BackgroundColor,
        bool IsBold,
        int ProjectCount);
}
