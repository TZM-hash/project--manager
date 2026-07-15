using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;
using ProjectType = ProjectManager.Web.Models.ProjectType;

namespace ProjectManager.Web.Pages.Admin.Projects;

[Authorize(Roles = RoleNames.BusinessManagerRoles)]
public sealed class ReportModel(
    ApplicationDbContext db,
    SystemSettingsService systemSettingsService,
    OpenCcConverterService openCcConverter) : PageModel
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

    [BindProperty(SupportsGet = true)]
    public ProjectType? ProjectType { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool OpenOnly { get; set; }

    [BindProperty(SupportsGet = true)]
    public int[]? SelectedIds { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReportType { get; set; }

    public IReadOnlyList<Project> Projects { get; private set; } = [];

    public int TotalProjectCount { get; private set; }

    public int EngineeringProjectCount { get; private set; }

    public int MaintenanceProjectCount { get; private set; }

    public decimal TotalProjectAmount { get; private set; }

    public SystemSettingsService.DisplayLanguage CurrentLanguage { get; private set; }

    public bool IsEngineeringDoingReport => ReportType == "engineering";

    public string ReportTitle
    {
        get
        {
            if (IsEngineeringDoingReport)
            {
                return Year.HasValue ? $"{Year}年度實施中工程專案報表" : "實施中工程專案報表";
            }
            return Year.HasValue ? $"{Year}年度專案報表" : "專案報表";
        }
    }

    public string GeneratedAt => DateTime.Now.ToString("yyyy-MM-dd HH:mm");

    public string Convert(string? text)
    {
        return openCcConverter.Convert(text, CurrentLanguage);
    }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        CurrentLanguage = await systemSettingsService.GetDisplayLanguageAsync(cancellationToken);

        IQueryable<Project> query = db.Projects
            .AsNoTracking()
            .Include(x => x.Status)
            .Include(x => x.Assignments).ThenInclude(x => x.User);

        if (IsEngineeringDoingReport)
        {
            query = query
                .Include(x => x.PurchaseRequests).ThenInclude(x => x!.VendorContact).ThenInclude(x => x!.Vendor)
                .Include(x => x.GanttPlan)
                .Where(x => x.ProjectType == Models.ProjectType.Engineering);
        }

        query = query.Where(x => !x.IsDeleted);

        if (SelectedIds is { Length: > 0 })
        {
            query = query.Where(x => SelectedIds.Contains(x.Id));
        }
        else
        {
            query = ApplyFilter(query);
        }

        Projects = await query
            .OrderBy(x => x.ProjectNumber)
            .ToListAsync(cancellationToken);

        TotalProjectCount = Projects.Count;
        EngineeringProjectCount = Projects.Count(x => x.ProjectType == Models.ProjectType.Engineering);
        MaintenanceProjectCount = Projects.Count(x => x.ProjectType == Models.ProjectType.Maintenance);
        TotalProjectAmount = Projects.Sum(x => x.ProjectAmount);
    }

    private IQueryable<Project> ApplyFilter(IQueryable<Project> query)
    {
        if (Year is not null)
        {
            query = query.Where(x => x.Year == Year);
        }

        if (!string.IsNullOrWhiteSpace(ParentCaseNumber))
        {
            query = query.Where(x => x.ParentCaseNumber != null &&
                                     x.ParentCaseNumber.Contains(ParentCaseNumber));
        }

        if (!string.IsNullOrWhiteSpace(ProjectNumber))
        {
            query = query.Where(x => x.ProjectNumber.Contains(ProjectNumber));
        }

        if (!string.IsNullOrWhiteSpace(ProjectName))
        {
            query = query.Where(x => x.Name.Contains(ProjectName));
        }

        if (!string.IsNullOrWhiteSpace(PersonnelUserId))
        {
            query = query.Where(x => x.Assignments.Any(a => a.UserId == PersonnelUserId));
        }

        if (StatusId is not null)
        {
            query = query.Where(x => x.StatusId == StatusId);
        }

        if (ProjectType is not null)
        {
            query = query.Where(x => x.ProjectType == ProjectType);
        }

        if (OpenOnly)
        {
            query = query.Where(x => x.Status != null && !x.Status.IsClosed);
        }

        if (IsEngineeringDoingReport)
        {
            var closedStatusCodes = new[] { "closed", "pending_collection" };
            query = query.Where(x => x.Status != null && !closedStatusCodes.Contains(x.Status.Code));
        }

        return query;
    }

    public string? GetVendorName(Project project)
    {
        var vendorContact = project.PurchaseRequests
            .Where(x => !x.IsDeleted && x.VendorContact != null)
            .Select(x => x.VendorContact)
            .FirstOrDefault();
        return vendorContact?.Vendor?.CompanyName ?? vendorContact?.Name ?? "-";
    }

    public string? GetExpectedTime(Project project)
    {
        if (project.GanttPlan?.StartDate != null && project.GanttPlan?.FinishDate != null)
        {
            return $"{project.GanttPlan.StartDate.Value:yyyy-MM-dd} ~ {project.GanttPlan.FinishDate.Value:yyyy-MM-dd}";
        }
        return "-";
    }

    public string? GetProgressDescription(Project project)
    {
        return project.ProgressDescription ?? "-";
    }
}
