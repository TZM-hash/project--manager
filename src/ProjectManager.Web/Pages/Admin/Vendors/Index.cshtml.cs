using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Pages.Shared;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Admin.Vendors;

[Authorize(Roles = RoleNames.Administrator)]
public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = PageSizeOptions.DefaultPageSize;

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    public IReadOnlyList<VendorListItem> Vendors { get; private set; } = [];

    public int TotalCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public PaginationViewModel Pagination => new(
        PageNumber,
        PageSize,
        TotalCount,
        TotalPages,
        "./Index",
        BuildRouteValues());

    public FilterSummaryViewModel FilterSummary => new(
        "./Index",
        BuildFilterSummaryItems(),
        new Dictionary<string, string?> { [nameof(PageSize)] = PageSize.ToString() });

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var query = ApplyFilter(db.Vendors.AsNoTracking().Include(x => x.Contacts));
        var page = await PagedResult<Vendor>.CreateAsync(
            query.OrderBy(x => x.CompanyName),
            PageNumber,
            PageSize,
            cancellationToken);

        Vendors = page.Items.Select(v => new VendorListItem(
            v.Id,
            v.CompanyName,
            v.Notes,
            v.Contacts.Where(c => !c.IsDeleted).ToList()
        )).ToList();

        TotalCount = page.TotalCount;
        PageNumber = page.PageNumber;
        PageSize = page.PageSize;
        TotalPages = page.TotalPages;
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        var vendor = await db.Vendors.Include(x => x.Contacts).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (vendor is null)
        {
            return NotFound();
        }

        vendor.IsDeleted = true;
        vendor.UpdatedAt = DateTimeOffset.UtcNow;
        foreach (var contact in vendor.Contacts)
        {
            contact.IsDeleted = true;
            contact.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return RedirectToPage("./Index", BuildRouteValuesWithPaging());
    }

    private IQueryable<Vendor> ApplyFilter(IQueryable<Vendor> query)
    {
        query = query.Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            var keyword = Keyword.ToLowerInvariant();
            query = query.Where(x =>
                x.CompanyName.ToLowerInvariant().Contains(keyword) ||
                x.Contacts.Any(c => !c.IsDeleted && c.Name.ToLowerInvariant().Contains(keyword)));
        }

        return query;
    }

    private Dictionary<string, string?> BuildRouteValues()
    {
        return new Dictionary<string, string?>
        {
            [nameof(Keyword)] = Keyword
        };
    }

    private Dictionary<string, string?> BuildRouteValuesWithPaging()
    {
        var values = BuildRouteValues();
        values[nameof(PageNumber)] = PageNumber.ToString();
        values[nameof(PageSize)] = PageSize.ToString();
        return values;
    }

    private IReadOnlyList<FilterSummaryItem> BuildFilterSummaryItems()
    {
        var items = new List<FilterSummaryItem>();
        if (!string.IsNullOrWhiteSpace(Keyword))
        {
            items.Add(new FilterSummaryItem("關鍵字", Keyword));
        }
        return items;
    }

    public sealed record VendorListItem(
        int Id,
        string CompanyName,
        string? Notes,
        IReadOnlyList<VendorContact> Contacts);
}