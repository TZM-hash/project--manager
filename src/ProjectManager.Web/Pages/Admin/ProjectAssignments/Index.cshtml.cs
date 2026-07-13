using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.ProjectAssignments;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class IndexModel(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? SelectedUserId { get; set; }

    public int CurrentYear { get; } = DateTime.Now.Year;

    public List<UserProjectCount> UserProjectCounts { get; private set; } = [];

    public List<Project> UserProjects { get; private set; } = [];

    public string? SelectedUserName { get; private set; }

    public IReadOnlyList<ChartSlice> BarSlices { get; private set; } = [];

    public IReadOnlyList<ChartSlice> PieSlices { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var users = await userManager.Users
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        var assignments = await db.ProjectAssignments
            .AsNoTracking()
            .Include(x => x.Project)
            .Where(x => x.Project != null && !x.Project.IsDeleted && x.Project.Year == CurrentYear)
            .ToListAsync(cancellationToken);

        UserProjectCounts = users
            .Select(u => new UserProjectCount(
                u.Id,
                string.IsNullOrWhiteSpace(u.DisplayName) ? u.UserName ?? u.Id : u.DisplayName,
                assignments.Count(a => a.UserId == u.Id)))
            .Where(x => x.ProjectCount > 0)
            .OrderByDescending(x => x.ProjectCount)
            .ThenBy(x => x.UserName)
            .ToList();

        BarSlices = ChartPalette.BuildBars(UserProjectCounts.Select(x => (x.UserName, (decimal)x.ProjectCount)));
        PieSlices = ChartPalette.BuildSlices(UserProjectCounts.Select(x => (x.UserName, (decimal)x.ProjectCount)));

        if (!string.IsNullOrWhiteSpace(SelectedUserId))
        {
            var selectedUser = users.FirstOrDefault(x => x.Id == SelectedUserId);
            SelectedUserName = selectedUser is null
                ? SelectedUserId
                : (string.IsNullOrWhiteSpace(selectedUser.DisplayName) ? selectedUser.UserName ?? selectedUser.Id : selectedUser.DisplayName);

            UserProjects = await db.Projects
                .AsNoTracking()
                .Include(x => x.Status)
                .Include(x => x.Assignments).ThenInclude(x => x.User)
                .Include(x => x.PurchaseRequests).ThenInclude(x => x.VendorContact).ThenInclude(x => x!.Vendor)
                .Where(x => !x.IsDeleted && x.Year == CurrentYear)
                .Where(x => x.Assignments.Any(a => a.UserId == SelectedUserId))
                .OrderBy(x => x.ProjectNumber)
                .ToListAsync(cancellationToken);
        }
    }

    public string GetVendorName(Project project)
    {
        var vendorContact = project.PurchaseRequests
            .FirstOrDefault(r => !r.IsDeleted && r.VendorContact != null)?.VendorContact;
        return vendorContact?.Vendor?.CompanyName ?? "-";
    }

    public string GetPersonnelNames(Project project)
    {
        return string.Join("；", project.Assignments.Select(x => x.User?.DisplayName ?? x.User?.UserName ?? x.UserId));
    }

    public record UserProjectCount(string UserId, string UserName, int ProjectCount);
}