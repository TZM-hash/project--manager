using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ProjectManager.Web.Models;
using ProjectManager.Web.Security;
using ProjectManager.Web.Services.Operations;

namespace ProjectManager.Web.Pages.Operations;

[Authorize]
public sealed class IndexModel(
    OperationJobService jobs,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public IReadOnlyList<OperationJob> Jobs { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User) ?? string.Empty;
        Jobs = await jobs.GetRecentAsync(
            userId,
            User.IsInRole(RoleNames.Administrator),
            50,
            cancellationToken);
    }

    public static string TypeText(OperationJobType type) => type switch
    {
        OperationJobType.FullExport => "全量資料匯出",
        OperationJobType.FullImport => "全量資料匯入",
        OperationJobType.ProjectBulkDelete => "專案批量刪除",
        OperationJobType.MaintenanceBulkDelete => "保養訂單批量刪除",
        _ => type.ToString()
    };

    public static string StatusText(OperationJobStatus status) => status switch
    {
        OperationJobStatus.Queued => "等待處理",
        OperationJobStatus.Running => "處理中",
        OperationJobStatus.Succeeded => "已完成",
        OperationJobStatus.Failed => "失敗",
        OperationJobStatus.Cancelled => "已取消",
        _ => status.ToString()
    };

    public static string StatusClass(OperationJobStatus status) => status switch
    {
        OperationJobStatus.Succeeded => "complete",
        OperationJobStatus.Failed => "blocked",
        OperationJobStatus.Running => "active",
        _ => "neutral"
    };
}
