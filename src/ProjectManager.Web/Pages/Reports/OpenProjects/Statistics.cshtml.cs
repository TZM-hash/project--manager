using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Reports.OpenProjects;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.Leader + "," + RoleNames.Viewer + "," + RoleNames.ProjectStaff)]
public sealed class StatisticsModel(
    ProjectQueryService projectQueryService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Year { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ParentCaseNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ProjectNumber { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ProjectName { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? PersonnelUserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? StatusId { get; set; }

    public IReadOnlyList<StatisticsRow> Rows { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var personnelUserId = User.IsInRole(RoleNames.ProjectStaff)
            ? userManager.GetUserId(User)
            : PersonnelUserId;

        var projects = await projectQueryService.GetProjectsAsync(
            new ProjectFilter(Year, ParentCaseNumber, ProjectNumber, ProjectName, personnelUserId, StatusId, OpenOnly: true),
            cancellationToken);

        Rows = projects
            .GroupBy(x => x.Status?.Name ?? string.Empty)
            .OrderBy(x => x.Key)
            .Select(group => new StatisticsRow(
                group.Key,
                group.Count(),
                group.Sum(x => x.ProjectAmount),
                group.SelectMany(x => x.PurchaseRequests.Where(p => !p.IsDeleted)).Sum(x => x.PurchaseAmount),
                group.SelectMany(x => x.PurchaseRequests.Where(p => !p.IsDeleted)).Sum(x => x.ActualPaidAmount),
                group.Average(x => x.CollectionPercent)))
            .ToList();
    }

    public sealed record StatisticsRow(
        string StatusName,
        int Count,
        decimal ProjectAmountTotal,
        decimal PurchaseAmountTotal,
        decimal ActualPaidAmountTotal,
        decimal AverageCollectionPercent);
}
