using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Pages.Shared;
using ProjectManager.Web.Security;

namespace ProjectManager.Web.Pages.Admin.ProjectsDoing;

[Authorize(Roles = RoleNames.BusinessManagerRoles)]
public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 50;

    public IReadOnlyList<Project> Projects { get; private set; } = [];

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public PaginationViewModel Pagination => new(
        PageNumber,
        PageSize,
        TotalCount,
        TotalPages,
        "/Admin/ProjectsDoing/Index",
        new Dictionary<string, string?>());

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var query = db.Projects
            .AsNoTracking()
            .Include(x => x.Status)
            .Include(x => x.Assignments).ThenInclude(x => x.User)
            .Include(x => x.PurchaseRequests).ThenInclude(x => x.VendorContact).ThenInclude(x => x!.Vendor)
            .Where(x => !x.IsDeleted && x.ProjectType == Models.ProjectType.Engineering)
            .Where(x => x.Status != null && x.Status.Name != "已結案" && x.Status.Name != "待收款");

        TotalCount = await query.CountAsync(cancellationToken);
        TotalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
        PageNumber = Math.Max(1, Math.Min(PageNumber, TotalPages));

        Projects = await query
            .OrderBy(x => x.ProjectNumber)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);
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
}
