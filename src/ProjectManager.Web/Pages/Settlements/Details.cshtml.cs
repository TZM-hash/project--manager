using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services;

namespace ProjectManager.Web.Pages.Settlements;

[Authorize(Roles = RoleNames.FullBusinessReadRoles)]
public sealed class DetailsModel(
    ApplicationDbContext db,
    ExcelReportService excelReportService) : PageModel
{
    public MonthlySettlementBatch Batch { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var batch = await LoadBatchAsync(id, cancellationToken);
        if (batch is null)
        {
            return NotFound();
        }

        Batch = batch;
        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync(int id, CancellationToken cancellationToken)
    {
        var file = await excelReportService.ExportSettlementAsync(id, cancellationToken);
        return File(file.Contents, file.ContentType, file.FileName);
    }

    private async Task<MonthlySettlementBatch?> LoadBatchAsync(
        int id,
        CancellationToken cancellationToken)
    {
        return await db.MonthlySettlementBatches
            .AsNoTracking()
            .Include(x => x.CreatedByUser)
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
