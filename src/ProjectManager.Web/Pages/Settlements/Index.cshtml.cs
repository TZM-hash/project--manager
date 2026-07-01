using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;

namespace ProjectManager.Web.Pages.Settlements;

[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.Leader)]
public sealed class IndexModel(ApplicationDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int? Year { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? Month { get; set; }

    public IReadOnlyList<MonthlySettlementBatch> Batches { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var query = db.MonthlySettlementBatches
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
            .Include(x => x.Items)
            .AsQueryable();

        if (Year is not null)
        {
            query = query.Where(x => x.Year == Year);
        }

        if (Month is not null)
        {
            query = query.Where(x => x.Month == Month);
        }

        Batches = await query
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ThenByDescending(x => x.BatchNumber)
            .ToListAsync(cancellationToken);
    }
}
