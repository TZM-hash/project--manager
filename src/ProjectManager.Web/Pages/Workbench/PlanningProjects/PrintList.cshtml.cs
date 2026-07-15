using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Workbench.PlanningProjects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.ProjectStaff + "," + RoleNames.Leader + "," + RoleNames.Viewer)]
public sealed class PrintListModel(
    PlanningProjectService planningProjectService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Ids { get; set; }

    public IReadOnlyList<PlanningProject> Projects { get; private set; } = [];

    public Dictionary<string, string> UserDisplayNames { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Ids))
        {
            return RedirectToPage("./Index");
        }

        var idList = Ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x.Trim(), out var v) ? v : (int?)null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToArray();

        if (idList.Length == 0)
        {
            return RedirectToPage("./Index");
        }

        Projects = await planningProjectService.GetPlanningProjectsByIdsAsync(idList, cancellationToken);
        if (!User.CanManageAllBusinessData())
        {
            var currentUserId = userManager.GetUserId(User);
            Projects = string.IsNullOrWhiteSpace(currentUserId)
                ? []
                : Projects.Where(project => (project.LeaderUserId ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Contains(currentUserId, StringComparer.Ordinal))
                    .ToList();
        }

        var users = await userManager.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        UserDisplayNames = users.ToDictionary(
            x => x.Id,
            x => string.IsNullOrWhiteSpace(x.DisplayName) ? x.UserName ?? x.Id : x.DisplayName);

        return Page();
    }

    public string GetLeaderDisplayNames(string? leaderUserId)
    {
        if (string.IsNullOrWhiteSpace(leaderUserId))
        {
            return "-";
        }

        var ids = leaderUserId.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var names = ids
            .Where(id => UserDisplayNames.TryGetValue(id, out _))
            .Select(id => UserDisplayNames[id])
            .ToList();
        return names.Count > 0 ? string.Join("、", names) : "-";
    }
}
