using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;

namespace ProjectManager.Web.Pages;

public class IndexModel(ApplicationDbContext db) : PageModel
{
    public int TotalProjectCount { get; private set; }

    public int OpenProjectCount { get; private set; }

    public int ActiveStatusCount { get; private set; }

    public int SettlementBatchCount { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        TotalProjectCount = await db.Projects
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        OpenProjectCount = await db.Projects
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.Status != null && !x.Status.IsClosed, cancellationToken);

        ActiveStatusCount = await db.ProjectStatuses
            .AsNoTracking()
            .CountAsync(x => x.IsActive, cancellationToken);

        SettlementBatchCount = await db.MonthlySettlementBatches
            .AsNoTracking()
            .CountAsync(cancellationToken);
    }
}
